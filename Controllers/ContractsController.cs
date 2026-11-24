using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Contracts;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize(Roles = "Landlord,Tenant")]
public sealed class ContractsController(AppDbContext db) : ControllerBase
{
    private const int MaxPageSize = 50;

    /// <summary>Danh sách hợp đồng của tài khoản hiện tại, lọc và phân trang tại SQL Server.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedContractsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetContracts(
        [FromQuery] RentalContractStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize ||
            page > int.MaxValue / pageSize ||
            (status.HasValue && !Enum.IsDefined(status.Value)))
            return BadRequest(new { message = "Tham số lọc hoặc phân trang không hợp lệ." });

        var isLandlord = User.IsInRole("Landlord");
        var isTenant = User.IsInRole("Tenant");
        IQueryable<RentalContract> query = db.RentalContracts.AsNoTracking();

        if (isLandlord && isTenant)
            query = query.Where(x => x.LandlordId == userId || x.TenantId == userId);
        else if (isLandlord)
            query = query.Where(x => x.LandlordId == userId);
        else
            query = query.Where(x => x.TenantId == userId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = page > totalPages
            ? new List<ContractListItem>()
            : await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ContractListItem(
                    x.Id,
                    x.ContractCode,
                    x.BookingId,
                    x.PropertyId,
                    x.Property.Title,
                    x.LandlordId,
                    x.Landlord.FullName,
                    x.TenantId,
                    x.Tenant.FullName,
                    x.StartDate,
                    x.EndDate,
                    x.MonthlyRent,
                    x.DepositAmount,
                    x.Status.ToString(),
                    x.CreatedAt))
                .ToListAsync(cancellationToken);

        return Ok(new PagedContractsResponse(
            items,
            page,
            pageSize,
            totalItems,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages));
    }

    /// <summary>Xem chi tiết hợp đồng chỉ khi tài khoản hiện tại là Tenant hoặc Landlord tham gia.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RentalContractDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContract(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var contract = await db.RentalContracts
            .AsNoTracking()
            .Include(x => x.Property)
            .Include(x => x.Landlord)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(
                x => x.Id == id && (x.LandlordId == userId || x.TenantId == userId),
                cancellationToken);

        if (contract is null)
            return NotFound(new { message = "Không tìm thấy hợp đồng." });

        return Ok(new RentalContractDetailResponse(
            ToResponse(contract),
            contract.Property.Title,
            contract.Landlord.FullName,
            contract.Tenant.FullName));
    }

    /// <summary>Tạo hợp đồng nháp từ Booking Accepted; Tenant hoặc Landlord tham gia đều có thể khởi tạo.</summary>
    [HttpPost("from-booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(RentalContractResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RentalContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateFromBooking(Guid bookingId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var booking = await db.Bookings
            .Include(x => x.Property)
            .SingleOrDefaultAsync(x => x.Id == bookingId, cancellationToken);

        if (booking is null)
            return NotFound(new { message = "Không tìm thấy yêu cầu đặt thuê." });

        if (booking.TenantId != userId && booking.Property.LandlordId != userId)
            return Forbid();

        if (booking.Status != BookingStatus.Accepted)
            return Conflict(new { message = "Chỉ yêu cầu đặt thuê đã được chấp nhận mới tạo được hợp đồng." });

        if (booking.EndDate <= booking.StartDate || booking.ProposedPrice <= 0)
            return Conflict(new { message = "Thông tin ngày thuê hoặc giá thuê của yêu cầu không hợp lệ." });

        var existing = await db.RentalContracts
            .SingleOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Ok(ToResponse(existing));
        }

        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var contract = new RentalContract
        {
            BookingId = booking.Id,
            PropertyId = booking.PropertyId,
            LandlordId = booking.Property.LandlordId,
            TenantId = booking.TenantId,
            ContractCode = $"HD-{now:yyyyMMdd}-{suffix}",
            StartDate = booking.StartDate,
            EndDate = booking.EndDate,
            MonthlyRent = booking.ProposedPrice,
            DepositAmount = 0,
            PaymentDueDay = 1,
            Status = RentalContractStatus.Draft,
            CreatedAt = now
        };

        db.RentalContracts.Add(contract);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.Entry(contract).State = EntityState.Detached;

            var concurrentContract = await db.RentalContracts
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

            if (concurrentContract is not null)
                return Ok(ToResponse(concurrentContract));

            throw;
        }

        return CreatedAtAction(
            nameof(GetContract),
            new { id = contract.Id },
            ToResponse(contract));
    }

    /// <summary>Chủ nhà chỉnh sửa nội dung hợp đồng khi hợp đồng còn Draft.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(RentalContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateContract(
        Guid id,
        [FromBody] UpdateRentalContractRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (request.EndDate <= request.StartDate)
            return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu." });

        var contract = await db.RentalContracts
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (contract is null)
            return NotFound(new { message = "Không tìm thấy hợp đồng." });

        if (contract.LandlordId != userId)
            return Forbid();

        if (contract.Status != RentalContractStatus.Draft)
            return Conflict(new { message = "Chỉ hợp đồng Draft mới được chỉnh sửa." });

        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.MonthlyRent = request.MonthlyRent;
        contract.DepositAmount = request.DepositAmount;
        contract.PaymentDueDay = request.PaymentDueDay;
        contract.Terms = string.IsNullOrWhiteSpace(request.Terms) ? null : request.Terms.Trim();

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(contract));
    }

    /// <summary>Chủ nhà gửi bản nháp cho hai bên xác nhận; không tự chuyển hợp đồng sang Active.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(RentalContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitForSignature(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var contract = await db.RentalContracts
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (contract is null)
            return NotFound(new { message = "Không tìm thấy hợp đồng." });

        if (contract.LandlordId != userId)
            return Forbid();

        if (contract.Status != RentalContractStatus.Draft)
            return Conflict(new { message = "Chỉ hợp đồng Draft mới được gửi xác nhận." });

        contract.Status = RentalContractStatus.AwaitingSignature;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(contract));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static RentalContractResponse ToResponse(RentalContract contract) =>
        new(
            contract.Id,
            contract.BookingId,
            contract.PropertyId,
            contract.LandlordId,
            contract.TenantId,
            contract.ContractCode,
            contract.StartDate,
            contract.EndDate,
            contract.MonthlyRent,
            contract.DepositAmount,
            contract.PaymentDueDay,
            contract.Terms,
            contract.ContractUrl,
            contract.Status.ToString(),
            contract.SignedAt,
            contract.CreatedAt);
}
