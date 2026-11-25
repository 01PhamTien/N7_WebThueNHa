using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Contracts;
using Web2_ThueNha.Models.Payments;
using Web2_ThueNha.DTOs.Payments;

using Web2_ThueNha.Services;
using Web2_ThueNha.Services.Payments;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize(Roles = "Landlord,Tenant")]
public sealed class InvoicesController(AppDbContext db, IPaymentService paymentService) : ControllerBase
{
    private const int MaxPageSize = 50;

    /// <summary>Danh sách hóa đơn của các hợp đồng mà tài khoản hiện tại tham gia.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedInvoicesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] InvoiceType? type,
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize ||
            page > int.MaxValue / pageSize ||
            (status.HasValue && !Enum.IsDefined(status.Value)) ||
            (type.HasValue && !Enum.IsDefined(type.Value)) ||
            (keyword is { Length: > 100 }))
            return BadRequest(new { message = "Bộ lọc hoặc tham số phân trang không hợp lệ." });

        await paymentService.EnsureScheduledInvoicesAsync(
            userId, User.IsInRole("Landlord"), User.IsInRole("Tenant"), cancellationToken);
        await MarkOverdueInvoicesAsync(userId, cancellationToken);

        var isLandlord = User.IsInRole("Landlord");
        var isTenant = User.IsInRole("Tenant");
        var query = db.Invoices.AsNoTracking().Where(x =>
            (isLandlord && x.Contract.LandlordId == userId) ||
            (isTenant && x.Contract.TenantId == userId));

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        if (type.HasValue)
            query = query.Where(x => x.InvoiceType == type.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x =>
                x.InvoiceCode.Contains(term) ||
                x.Contract.ContractCode.Contains(term) ||
                x.Contract.Property.Title.Contains(term));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = page > totalPages
            ? new List<InvoiceListItem>()
            : await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.DueDate)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new InvoiceListItem(
                    x.Id,
                    x.InvoiceCode,
                    x.ContractId,
                    x.Contract.ContractCode,
                    x.Contract.PropertyId,
                    x.Contract.Property.Title,
                    x.Contract.LandlordId,
                    x.Contract.TenantId,
                    x.InvoiceType.ToString(),
                    x.IssueDate,
                    x.DueDate,
                    x.TotalAmount,
                    x.Status.ToString(),
                    x.CreatedAt))
                .ToListAsync(cancellationToken);

        return Ok(new PagedInvoicesResponse(
            items, page, pageSize, totalItems, totalPages,
            page > 1 && page <= totalPages, page < totalPages));
    }

    /// <summary>Xem chi tiết hóa đơn, dòng hóa đơn và các lần ghi nhận thanh toán.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var invoice = await db.Invoices
            .Include(x => x.Contract)
                .ThenInclude(x => x.Property)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (invoice is null ||
            (invoice.Contract.LandlordId != userId && invoice.Contract.TenantId != userId))
            return NotFound(new { message = "Không tìm thấy hóa đơn." });

        await paymentService.RefreshOverdueStatusAsync(invoice, cancellationToken);

        return Ok(ToDetail(invoice));
    }

    /// <summary>Chủ nhà phát hành hóa đơn cho hợp đồng đã Active.</summary>
    [HttpPost("/api/contracts/{contractId:guid}/invoices")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(InvoiceDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateInvoice(
        Guid contractId,
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await paymentService.CreateInvoiceAsync(contractId, userId, request, cancellationToken);
        if (result.StatusCode == StatusCodes.Status201Created && result.Data is not null)
            return CreatedAtAction(nameof(GetInvoice), new { id = result.Data.Id }, result.Data);

        return ToActionResult(result);
    }

    private Task MarkOverdueInvoicesAsync(Guid userId, CancellationToken cancellationToken) =>
        paymentService.MarkOverdueInvoicesAsync(
            userId,
            User.IsInRole("Landlord"),
            User.IsInRole("Tenant"),
            cancellationToken);


    private IActionResult ToActionResult<T>(ServiceResult<T> result) =>
        result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(result.Data),
            StatusCodes.Status400BadRequest => BadRequest(new { message = result.Message }),
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = result.Message }),
            StatusCodes.Status409Conflict => Conflict(new { message = result.Message }),
            _ => StatusCode(result.StatusCode, result.Message is null ? null : new { message = result.Message })
        };

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static InvoiceDetailResponse ToDetail(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.ContractId,
            invoice.Contract.ContractCode,
            invoice.Contract.PropertyId,
            invoice.Contract.Property.Title,
            invoice.Contract.LandlordId,
            invoice.Contract.TenantId,
            invoice.InvoiceCode,
            invoice.InvoiceType.ToString(),
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.TotalAmount,
            invoice.Status.ToString(),
            invoice.CreatedAt,
            invoice.Items.Select(x => new InvoiceItemResponse(
                x.Id, x.Description, x.Quantity, x.UnitPrice, x.Amount)).ToList(),
            invoice.Payments.OrderBy(x => x.CreatedAt).Select(PaymentService.ToResponse).ToList());
}
