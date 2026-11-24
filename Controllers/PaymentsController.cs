using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Payments;
using Web2_ThueNha.DTOs.Payments;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class PaymentsController(AppDbContext db) : ControllerBase
{
    /// <summary>Người thuê gửi yêu cầu ghi nhận thanh toán tiền mặt hoặc chuyển khoản.</summary>
    [HttpPost("invoices/{invoiceId:guid}/payments")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePayment(
        Guid invoiceId,
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!Enum.IsDefined(request.PaymentMethod) ||
            request.PaymentMethod is not (PaymentMethod.Cash or PaymentMethod.BankTransfer) ||
            request.Amount <= 0 ||
            decimal.Round(request.Amount, 2) != request.Amount ||
            (request.TransactionCode?.Trim().Length ?? 0) > 150)
            return BadRequest(new { message = "Chỉ hỗ trợ Cash/BankTransfer; số tiền và mã giao dịch phải hợp lệ." });

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var invoice = await db.Invoices
            .Include(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

        if (invoice is null || invoice.Contract.TenantId != userId)
            return NotFound(new { message = "Không tìm thấy hóa đơn." });

        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled or InvoiceStatus.Paid)
            return Conflict(new { message = "Hóa đơn không ở trạng thái được phép ghi nhận thanh toán." });

        var reservedAmount = await db.Payments
            .Where(x => x.InvoiceId == invoiceId &&
                (x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Succeeded))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        if (reservedAmount + request.Amount > invoice.TotalAmount)
            return Conflict(new { message = "Số tiền yêu cầu vượt quá số dư hóa đơn còn lại." });

        var transactionCode = string.IsNullOrWhiteSpace(request.TransactionCode)
            ? null
            : request.TransactionCode.Trim();

        if (transactionCode is not null &&
            await db.Payments.AnyAsync(x => x.TransactionCode == transactionCode, cancellationToken))
            return Conflict(new { message = "Mã giao dịch đã được sử dụng." });

        var payment = new Payment
        {
            InvoiceId = invoiceId,
            PayerId = userId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaymentProvider = "Manual",
            TransactionCode = transactionCode,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.Payments.Add(payment);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            return Conflict(new { message = "Mã giao dịch đã được ghi nhận." });
        }

        return StatusCode(StatusCodes.Status201Created, ToResponse(payment));
    }

    /// <summary>Chủ nhà của hợp đồng hoặc Admin xác nhận khoản thanh toán đang chờ.</summary>
    [HttpPatch("payments/{paymentId:guid}/confirm")]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmPayment(Guid paymentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var payment = await db.Payments
            .Include(x => x.Invoice)
                .ThenInclude(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
            return NotFound(new { message = "Không tìm thấy khoản thanh toán." });

        if (!User.IsInRole("Admin") && payment.Invoice.Contract.LandlordId != userId)
            return Forbid();

        if (payment.Status != PaymentStatus.Pending ||
            payment.Invoice.Status == InvoiceStatus.Cancelled)
            return Conflict(new { message = "Khoản thanh toán không còn ở trạng thái chờ xác nhận." });

        var paidBefore = await db.Payments
            .Where(x => x.InvoiceId == payment.InvoiceId &&
                x.Id != paymentId &&
                x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        if (paidBefore + payment.Amount > payment.Invoice.TotalAmount)
            return Conflict(new { message = "Xác nhận khoản này sẽ làm tổng thanh toán vượt hóa đơn." });

        var now = DateTime.UtcNow;
        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAt = now;
        payment.ReviewedById = userId;
        payment.ReviewedAt = now;
        payment.FailureReason = null;

        var totalPaid = paidBefore + payment.Amount;
        payment.Invoice.Status = CalculateInvoiceStatus(payment.Invoice, totalPaid);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ToResponse(payment));
    }

    /// <summary>Chủ nhà của hợp đồng hoặc Admin từ chối khoản thanh toán và lưu lý do.</summary>
    [HttpPatch("payments/{paymentId:guid}/reject")]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectPayment(
        Guid paymentId,
        [FromBody] RejectPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var payment = await db.Payments
            .Include(x => x.Invoice)
                .ThenInclude(x => x.Contract)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
            return NotFound(new { message = "Không tìm thấy khoản thanh toán." });

        if (!User.IsInRole("Admin") && payment.Invoice.Contract.LandlordId != userId)
            return Forbid();

        if (payment.Status != PaymentStatus.Pending)
            return Conflict(new { message = "Chỉ khoản thanh toán đang chờ mới được từ chối." });

        var reason = request.Reason.Trim();
        if (reason.Length == 0)
            return BadRequest(new { message = "Lý do từ chối không được để trống." });

        var now = DateTime.UtcNow;
        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = reason;
        payment.ReviewedById = userId;
        payment.ReviewedAt = now;

        var totalPaid = await db.Payments
            .Where(x => x.InvoiceId == payment.InvoiceId &&
                x.Id != paymentId &&
                x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        payment.Invoice.Status = CalculateInvoiceStatus(payment.Invoice, totalPaid);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ToResponse(payment));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static InvoiceStatus CalculateInvoiceStatus(Invoice invoice, decimal totalPaid)
    {
        if (totalPaid >= invoice.TotalAmount)
            return InvoiceStatus.Paid;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (invoice.DueDate < today)
            return InvoiceStatus.Overdue;

        return totalPaid > 0
            ? InvoiceStatus.PartiallyPaid
            : InvoiceStatus.Issued;
    }

    public static PaymentResponse ToResponse(Payment payment) =>
        new(
            payment.Id,
            payment.InvoiceId,
            payment.PayerId,
            payment.Amount,
            payment.PaymentMethod.ToString(),
            payment.Status.ToString(),
            payment.PaymentProvider,
            payment.TransactionCode,
            payment.PaidAt,
            payment.FailureReason,
            payment.ReviewedById,
            payment.ReviewedAt,
            payment.CreatedAt);
}
