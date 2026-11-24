using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Contracts;
using Web2_ThueNha.Models.Payments;
using Web2_ThueNha.Services.Notifications;
using Web2_ThueNha.DTOs.Payments;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize(Roles = "Landlord,Tenant")]
public sealed class InvoicesController(AppDbContext db, INotificationService notifications) : ControllerBase
{
    private const int MaxPageSize = 50;

    /// <summary>Danh sách hóa đơn của các hợp đồng mà tài khoản hiện tại tham gia.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedInvoicesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] InvoiceType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize ||
            page > int.MaxValue / pageSize ||
            (status.HasValue && !Enum.IsDefined(status.Value)) ||
            (type.HasValue && !Enum.IsDefined(type.Value)))
            return BadRequest(new { message = "Bộ lọc hoặc tham số phân trang không hợp lệ." });

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

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = page > totalPages
            ? new List<InvoiceListItem>()
            : await query
                .OrderByDescending(x => x.IssueDate)
                .ThenByDescending(x => x.CreatedAt)
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

        if (invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid &&
            invoice.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            invoice.Status = InvoiceStatus.Overdue;
            await db.SaveChangesAsync(cancellationToken);
        }

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

        if (!Enum.IsDefined(request.InvoiceType) ||
            request.IssueDate == default ||
            request.DueDate == default ||
            request.DueDate < request.IssueDate ||
            request.Items is null ||
            request.Items.Count is < 1 or > 20 ||
            request.Items.Any(x =>
                string.IsNullOrWhiteSpace(x.Description) ||
                x.Description.Trim().Length > 300 ||
                x.Quantity <= 0 ||
                x.UnitPrice <= 0 ||
                decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero) <= 0))
            return BadRequest(new { message = "Thông tin hóa đơn hoặc dòng chi tiết không hợp lệ." });

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var contract = await db.RentalContracts
            .SingleOrDefaultAsync(x => x.Id == contractId, cancellationToken);

        if (contract is null)
            return NotFound(new { message = "Không tìm thấy hợp đồng." });

        if (contract.LandlordId != userId)
            return Forbid();

        if (contract.Status != RentalContractStatus.Active)
            return Conflict(new { message = "Chỉ hợp đồng đã được cả hai bên xác nhận mới phát hành hóa đơn." });

        if (request.InvoiceType == InvoiceType.Deposit)
        {
            if (request.PeriodStart.HasValue || request.PeriodEnd.HasValue)
                return BadRequest(new { message = "Hóa đơn tiền cọc không có kỳ thanh toán." });

            if (contract.DepositAmount <= 0)
                return Conflict(new { message = "Hợp đồng không có khoản tiền cọc cần thu." });
        }
        else if (request.InvoiceType == InvoiceType.MonthlyRent)
        {
            if (!request.PeriodStart.HasValue || !request.PeriodEnd.HasValue ||
                request.PeriodEnd < request.PeriodStart ||
                request.PeriodStart < contract.StartDate ||
                request.PeriodEnd > contract.EndDate)
                return BadRequest(new { message = "Kỳ hóa đơn tiền thuê phải nằm trong thời hạn hợp đồng." });
        }
        else if (request.PeriodStart.HasValue != request.PeriodEnd.HasValue ||
                 (request.PeriodStart.HasValue && request.PeriodEnd < request.PeriodStart))
        {
            return BadRequest(new { message = "Khoảng thời gian hóa đơn không hợp lệ." });
        }

        var totalAmount = request.Items.Sum(x =>
            decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero));

        if (totalAmount <= 0 || totalAmount > 9999999999999999.99m)
            return BadRequest(new { message = "Tổng hóa đơn phải lớn hơn 0 và không vượt giới hạn decimal(18,2)." });

        if (request.InvoiceType == InvoiceType.Deposit && totalAmount != contract.DepositAmount)
            return BadRequest(new { message = "Tổng hóa đơn tiền cọc phải bằng tiền cọc trong hợp đồng." });

        var duplicate = request.InvoiceType switch
        {
            InvoiceType.Deposit => await db.Invoices.AnyAsync(x =>
                x.ContractId == contractId &&
                x.InvoiceType == InvoiceType.Deposit &&
                x.Status != InvoiceStatus.Cancelled, cancellationToken),
            InvoiceType.MonthlyRent => await db.Invoices.AnyAsync(x =>
                x.ContractId == contractId &&
                x.InvoiceType == InvoiceType.MonthlyRent &&
                x.PeriodStart == request.PeriodStart &&
                x.PeriodEnd == request.PeriodEnd &&
                x.Status != InvoiceStatus.Cancelled, cancellationToken),
            _ => false
        };

        if (duplicate)
            return Conflict(new { message = "Hóa đơn cùng loại và kỳ thanh toán đã tồn tại." });

        var invoiceCode = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var invoice = new Invoice
        {
            ContractId = contractId,
            InvoiceCode = invoiceCode,
            InvoiceType = request.InvoiceType,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            IssueDate = request.IssueDate,
            DueDate = request.DueDate,
            TotalAmount = totalAmount,
            Status = request.DueDate < DateOnly.FromDateTime(DateTime.UtcNow)
                ? InvoiceStatus.Overdue
                : InvoiceStatus.Issued,
            CreatedAt = DateTime.UtcNow,
            Items = request.Items.Select(x => new InvoiceItem
            {
                Description = x.Description.Trim(),
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Amount = decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero)
            }).ToList()
        };

        db.Invoices.Add(invoice);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await notifications.CreateAsync(
                contract.TenantId,
                "Hóa đơn mới được phát hành",
                $"Bạn có hóa đơn {invoice.InvoiceCode} cần thanh toán.",
                NotificationType.Invoice,
                invoice.Id,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            return Conflict(new { message = "Hóa đơn trùng loại/kỳ thanh toán đã được tạo bởi một request khác." });
        }

        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, ToDetail(invoice));
    }

    private async Task MarkOverdueInvoicesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isLandlord = User.IsInRole("Landlord");
        var isTenant = User.IsInRole("Tenant");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await db.Invoices
            .Where(x =>
                ((isLandlord && x.Contract.LandlordId == userId) ||
                 (isTenant && x.Contract.TenantId == userId)) &&
                x.DueDate < today &&
                (x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.PartiallyPaid))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Status, InvoiceStatus.Overdue),
                cancellationToken);
    }

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
            invoice.Payments.OrderBy(x => x.CreatedAt).Select(PaymentsController.ToResponse).ToList());
}
