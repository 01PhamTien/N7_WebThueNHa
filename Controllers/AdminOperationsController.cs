using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Admin;
using Web2_ThueNha.DTOs.Bookings;
using Web2_ThueNha.DTOs.Contracts;
using Web2_ThueNha.DTOs.Payments;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Bookings;
using Web2_ThueNha.Models.Contracts;
using Web2_ThueNha.Models.Payments;
using Web2_ThueNha.Models.Interactions;
using Web2_ThueNha.Models.Moderation;

namespace Web2_ThueNha.Controllers;

/// <summary>
/// Các màn hình giám sát vận hành toàn hệ thống dành riêng cho Admin.
/// Đây là các truy vấn chỉ đọc; thao tác nghiệp vụ vẫn đi qua API chuyên trách.
/// </summary>
[ApiController]
[Route("api/admin/operations")]
[Authorize(Roles = "Admin")]
public sealed class AdminOperationsController(AppDbContext db) : ControllerBase
{
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings(
        [FromQuery] BookingStatus? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPage(page, pageSize) ||
            (status.HasValue && !Enum.IsDefined(status.Value)) ||
            keyword is { Length: > 100 })
            return BadRequest(new { message = "Bộ lọc hoặc phân trang yêu cầu thuê không hợp lệ." });

        IQueryable<Booking> query = db.Bookings.AsNoTracking();
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.Property.Title.Contains(term) ||
                                     x.Tenant.FullName.Contains(term) ||
                                     x.Tenant.Email.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = page > TotalPages(total, pageSize)
            ? new List<BookingResponse>()
            : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new BookingResponse(
                    x.Id, x.PropertyId, x.Property.Title, x.TenantId,
                    x.Tenant.FullName, x.Tenant.Email, x.StartDate, x.EndDate,
                    x.NumberOfPeople, x.ProposedPrice, x.Message, x.Status,
                    x.RespondedAt, x.RejectionReason, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(cancellationToken);

        return Ok(ToPage(items, page, pageSize, total));
    }

    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts(
        [FromQuery] RentalContractStatus? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPage(page, pageSize) ||
            (status.HasValue && !Enum.IsDefined(status.Value)) ||
            keyword is { Length: > 100 })
            return BadRequest(new { message = "Bộ lọc hoặc phân trang hợp đồng không hợp lệ." });

        IQueryable<RentalContract> query = db.RentalContracts.AsNoTracking();
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.ContractCode.Contains(term) ||
                                     x.Property.Title.Contains(term) ||
                                     x.Landlord.FullName.Contains(term) ||
                                     x.Tenant.FullName.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = page > TotalPages(total, pageSize)
            ? new List<ContractListItem>()
            : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new ContractListItem(
                    x.Id, x.ContractCode, x.BookingId, x.PropertyId, x.Property.Title,
                    x.LandlordId, x.Landlord.FullName, x.TenantId, x.Tenant.FullName,
                    x.StartDate, x.EndDate, x.MonthlyRent, x.DepositAmount,
                    x.Status.ToString(), x.CreatedAt, x.LandlordSignedAt, x.TenantSignedAt))
                .ToListAsync(cancellationToken);

        return Ok(ToPage(items, page, pageSize, total));
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] InvoiceType? type,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPage(page, pageSize) ||
            (status.HasValue && !Enum.IsDefined(status.Value)) ||
            (type.HasValue && !Enum.IsDefined(type.Value)) ||
            keyword is { Length: > 100 })
            return BadRequest(new { message = "Bộ lọc hoặc phân trang hóa đơn không hợp lệ." });

        IQueryable<Invoice> query = db.Invoices.AsNoTracking();
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        if (type.HasValue)
            query = query.Where(x => x.InvoiceType == type.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.InvoiceCode.Contains(term) ||
                                     x.Contract.ContractCode.Contains(term) ||
                                     x.Contract.Property.Title.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = page > TotalPages(total, pageSize)
            ? new List<InvoiceListItem>()
            : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new InvoiceListItem(
                    x.Id, x.InvoiceCode, x.ContractId, x.Contract.ContractCode,
                    x.Contract.PropertyId, x.Contract.Property.Title,
                    x.Contract.LandlordId, x.Contract.TenantId, x.InvoiceType.ToString(),
                    x.PeriodStart, x.PeriodEnd, x.IssueDate, x.DueDate,
                    x.TotalAmount, x.Status.ToString(), x.CreatedAt))
                .ToListAsync(cancellationToken);

        return Ok(ToPage(items, page, pageSize, total));
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments(
        [FromQuery] PaymentStatus? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPage(page, pageSize) ||
            (status.HasValue && !Enum.IsDefined(status.Value)) ||
            keyword is { Length: > 100 })
            return BadRequest(new { message = "Bộ lọc hoặc phân trang thanh toán không hợp lệ." });

        IQueryable<Payment> query = db.Payments.AsNoTracking();
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
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
        var items = page > TotalPages(total, pageSize)
            ? new List<AdminPaymentListItem>()
            : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new AdminPaymentListItem(
                    x.Id, x.InvoiceId, x.Invoice.InvoiceCode, x.Invoice.Contract.ContractCode,
                    x.Invoice.Contract.Property.Title, x.PayerId, x.Payer.FullName,
                    x.Amount, x.PaymentMethod.ToString(), x.Status.ToString(),
                    x.PaymentProvider, x.TransactionCode, x.PaidAt, x.FailureReason,
                    x.ReviewedBy == null ? null : x.ReviewedBy.FullName, x.ReviewedAt, x.CreatedAt))
                .ToListAsync(cancellationToken);

        return Ok(ToPage(items, page, pageSize, total));
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews(
        [FromQuery] int? rating,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPage(page, pageSize) ||
            (rating.HasValue && (rating.Value < 1 || rating.Value > 5)) ||
            keyword is { Length: > 100 })
            return BadRequest(new { message = "Bộ lọc hoặc phân trang đánh giá không hợp lệ." });

        IQueryable<Review> query = db.Reviews.AsNoTracking();
        if (rating.HasValue)
            query = query.Where(x => x.Rating == rating.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.Contract.ContractCode.Contains(term) ||
                                     x.Contract.Property.Title.Contains(term) ||
                                     x.Contract.Tenant.FullName.Contains(term) ||
                                     (x.Comment != null && x.Comment.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = page > TotalPages(total, pageSize)
            ? new List<AdminReviewListItem>()
            : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new AdminReviewListItem(
                    x.Id, x.ContractId, x.Contract.ContractCode,
                    x.Contract.Property.Title, x.Contract.Tenant.FullName,
                    x.Rating, x.Comment, x.CreatedAt))
                .ToListAsync(cancellationToken);

        return Ok(ToPage(items, page, pageSize, total));
    }

    [HttpGet("moderation-logs")]
    public async Task<IActionResult> GetModerationLogs(
        [FromQuery] ModerationAction? action,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPage(page, pageSize) ||
            (action.HasValue && !Enum.IsDefined(action.Value)) ||
            keyword is { Length: > 100 })
            return BadRequest(new { message = "Bộ lọc hoặc phân trang nhật ký không hợp lệ." });

        IQueryable<PropertyModerationLog> query = db.PropertyModerationLogs.AsNoTracking();
        if (action.HasValue)
            query = query.Where(x => x.Action == action.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.Property.Title.Contains(term) ||
                                     x.Admin.FullName.Contains(term) ||
                                     (x.Reason != null && x.Reason.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = page > TotalPages(total, pageSize)
            ? new List<AdminModerationLogListItem>()
            : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new AdminModerationLogListItem(
                    x.Id, x.PropertyId, x.Property.Title, x.Admin.FullName,
                    x.Action.ToString(), x.Reason, x.CreatedAt))
                .ToListAsync(cancellationToken);

        return Ok(ToPage(items, page, pageSize, total));
    }

    private static bool ValidPage(int page, int pageSize) =>
        page >= 1 && pageSize >= 1 && pageSize <= 100 && page <= int.MaxValue / pageSize;

    private static int TotalPages(int total, int pageSize) =>
        (int)Math.Ceiling(total / (double)pageSize);

    private static object ToPage<T>(IReadOnlyList<T> items, int page, int pageSize, int total) =>
        new
        {
            items,
            page,
            pageSize,
            totalItems = total,
            totalPages = TotalPages(total, pageSize),
            hasPrevious = page > 1 && page <= TotalPages(total, pageSize),
            hasNext = page < TotalPages(total, pageSize)
        };
}
