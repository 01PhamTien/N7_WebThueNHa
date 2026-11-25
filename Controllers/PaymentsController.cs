using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web2_ThueNha.DTOs.Payments;
using Web2_ThueNha.DTOs.Admin;
using Web2_ThueNha.Data;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Models.Payments;
using Web2_ThueNha.Services;
using Web2_ThueNha.Services.Payments;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class PaymentsController(IPaymentService paymentService, AppDbContext db) : ControllerBase
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

        return ToActionResult(await paymentService.CreatePaymentAsync(invoiceId, userId, request, cancellationToken));
    }

    /// <summary>Chủ nhà xem toàn bộ giao dịch thuộc các hợp đồng của mình.</summary>
    [HttpGet("payments/incoming")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> GetIncomingPayments(
        [FromQuery] PaymentStatus? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 50 || page > int.MaxValue / pageSize ||
            (status.HasValue && !Enum.IsDefined(status.Value)) || keyword is { Length: > 100 })
            return BadRequest(new { message = "Bộ lọc hoặc phân trang thanh toán không hợp lệ." });

        var landlordId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
        if (landlordId == Guid.Empty) return Unauthorized();

        var query = db.Payments.AsNoTracking().Where(x => x.Invoice.Contract.LandlordId == landlordId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.Invoice.InvoiceCode.Contains(term) ||
                x.Invoice.Contract.ContractCode.Contains(term) ||
                x.Invoice.Contract.Property.Title.Contains(term) ||
                x.Payer.FullName.Contains(term) ||
                (x.TransactionCode != null && x.TransactionCode.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminPaymentListItem(
                x.Id, x.InvoiceId, x.Invoice.InvoiceCode, x.Invoice.Contract.ContractCode,
                x.Invoice.Contract.Property.Title, x.PayerId, x.Payer.FullName,
                x.Amount, x.PaymentMethod.ToString(), x.Status.ToString(),
                x.PaymentProvider, x.TransactionCode, x.PaidAt, x.FailureReason,
                x.ReviewedBy == null ? null : x.ReviewedBy.FullName, x.ReviewedAt, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return Ok(new { items, page, pageSize, totalItems = total, totalPages,
            hasPrevious = page > 1 && page <= totalPages, hasNext = page < totalPages });
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

        return ToActionResult(await paymentService.ConfirmPaymentAsync(
            paymentId, userId, User.IsInRole("Admin"), cancellationToken));
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

        return ToActionResult(await paymentService.RejectPaymentAsync(
            paymentId, userId, User.IsInRole("Admin"), request, cancellationToken));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private IActionResult ToActionResult<T>(ServiceResult<T> result) =>
        result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(result.Data),
            StatusCodes.Status201Created => StatusCode(StatusCodes.Status201Created, result.Data),
            StatusCodes.Status400BadRequest => BadRequest(new { message = result.Message }),
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = result.Message }),
            StatusCodes.Status409Conflict => Conflict(new { message = result.Message }),
            _ => StatusCode(result.StatusCode, result.Message is null ? null : new { message = result.Message })
        };

    public static PaymentResponse ToResponse(Payment payment) => PaymentService.ToResponse(payment);
}
