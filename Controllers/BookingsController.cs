using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Bookings;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public sealed class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BookingsController(AppDbContext db) => _db = db;

    /// <summary>Người thuê gửi yêu cầu đặt thuê cho một căn nhà đang được đăng.</summary>
    [HttpPost]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var tenantId))
            return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.StartDate < today)
            return BadRequest(new { message = "Ngày bắt đầu không được ở trong quá khứ." });

        if (request.EndDate <= request.StartDate)
            return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu." });

        var property = await _db.Properties
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.PropertyId, cancellationToken);

        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });

        if (property.LandlordId == tenantId)
            return BadRequest(new { message = "Chủ nhà không thể tự gửi yêu cầu thuê căn nhà của mình." });

        if (property.ModerationStatus != PropertyModerationStatus.Approved ||
            property.ListingStatus != PropertyListingStatus.Published)
            return Conflict(new { message = "Căn nhà hiện không ở trạng thái được phép nhận yêu cầu thuê." });

        var hasAcceptedConflict = await _db.Bookings.AnyAsync(x =>
            x.PropertyId == request.PropertyId &&
            x.Status == BookingStatus.Accepted &&
            x.StartDate < request.EndDate &&
            x.EndDate > request.StartDate, cancellationToken);

        if (hasAcceptedConflict)
            return Conflict(new { message = "Khoảng thời gian này đã có yêu cầu thuê được chấp nhận." });

        var booking = new Booking
        {
            PropertyId = property.Id,
            TenantId = tenantId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfPeople = request.NumberOfPeople,
            ProposedPrice = request.ProposedPrice,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(cancellationToken);

        var response = await GetBookingResponseQuery()
            .SingleAsync(x => x.Id == booking.Id, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, response);
    }

    /// <summary>Người thuê xem các yêu cầu đặt thuê do mình tạo.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(PagedResponse<BookingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] BookingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var tenantId))
            return Unauthorized();

        if (!IsValidPaging(page, pageSize))
            return BadRequest(new { message = "page phải lớn hơn hoặc bằng 1; pageSize phải từ 1 đến 50." });

        if (status.HasValue && !Enum.IsDefined(status.Value))
            return BadRequest(new { message = "Trạng thái yêu cầu thuê không hợp lệ." });

        var query = _db.Bookings.AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await CreatePagedResponse(query, page, pageSize, cancellationToken);
    }

    /// <summary>Chủ nhà xem yêu cầu gửi đến các căn nhà thuộc sở hữu của mình.</summary>
    [HttpGet("incoming")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(PagedResponse<BookingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncoming(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] BookingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var landlordId))
            return Unauthorized();

        if (!IsValidPaging(page, pageSize))
            return BadRequest(new { message = "page phải lớn hơn hoặc bằng 1; pageSize phải từ 1 đến 50." });

        if (status.HasValue && !Enum.IsDefined(status.Value))
            return BadRequest(new { message = "Trạng thái yêu cầu thuê không hợp lệ." });

        var query = _db.Bookings.AsNoTracking()
            .Where(x => _db.Properties.Any(p =>
                p.Id == x.PropertyId && p.LandlordId == landlordId));

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await CreatePagedResponse(query, page, pageSize, cancellationToken);
    }

    /// <summary>Xem chi tiết yêu cầu nếu là người thuê tạo yêu cầu hoặc chủ nhà sở hữu căn nhà.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var booking = await GetBookingResponseQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (booking is null)
            return NotFound(new { message = "Không tìm thấy yêu cầu đặt thuê." });

        var isTenant = booking.TenantId == userId;
        var isLandlord = await _db.Properties.AnyAsync(
            x => x.Id == booking.PropertyId && x.LandlordId == userId, cancellationToken);
        var isAdmin = User.IsInRole("Admin");

        if (!isTenant && !isLandlord && !isAdmin)
            return Forbid();

        return Ok(booking);
    }

    /// <summary>Chủ nhà chấp nhận yêu cầu; kiểm tra lại xung đột lịch trong transaction.</summary>
    [HttpPatch("{id:guid}/accept")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var landlordId))
            return Unauthorized();

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var booking = await _db.Bookings
            .Include(x => x.Property)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (booking is null)
            return NotFound(new { message = "Không tìm thấy yêu cầu đặt thuê." });

        if (booking.Property.LandlordId != landlordId)
            return Forbid();

        if (booking.Status != BookingStatus.Pending)
            return Conflict(new { message = "Chỉ yêu cầu đang chờ mới được chấp nhận." });

        if (booking.Property.ModerationStatus != PropertyModerationStatus.Approved ||
            booking.Property.ListingStatus != PropertyListingStatus.Published)
            return Conflict(new { message = "Căn nhà không còn ở trạng thái được phép cho thuê." });

        var hasAcceptedConflict = await _db.Bookings.AnyAsync(x =>
            x.Id != booking.Id &&
            x.PropertyId == booking.PropertyId &&
            x.Status == BookingStatus.Accepted &&
            x.StartDate < booking.EndDate &&
            x.EndDate > booking.StartDate, cancellationToken);

        if (hasAcceptedConflict)
            return Conflict(new { message = "Không thể chấp nhận vì khoảng thời gian này đã có yêu cầu thuê được chấp nhận." });

        booking.Status = BookingStatus.Accepted;
        booking.RespondedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(ToResponse(booking));
    }

    /// <summary>Chủ nhà từ chối yêu cầu đang chờ.</summary>
    [HttpPatch("{id:guid}/reject")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(
        Guid id, RejectBookingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var landlordId))
            return Unauthorized();

        var booking = await _db.Bookings
            .Include(x => x.Property)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (booking is null)
            return NotFound(new { message = "Không tìm thấy yêu cầu đặt thuê." });

        if (booking.Property.LandlordId != landlordId)
            return Forbid();

        if (booking.Status != BookingStatus.Pending)
            return Conflict(new { message = "Chỉ yêu cầu đang chờ mới được từ chối." });

        booking.Status = BookingStatus.Rejected;
        booking.RejectionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        booking.RespondedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(booking));
    }

    /// <summary>Người thuê hủy yêu cầu của chính mình khi yêu cầu vẫn đang chờ.</summary>
    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var tenantId))
            return Unauthorized();

        var booking = await _db.Bookings
            .Include(x => x.Property)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (booking is null)
            return NotFound(new { message = "Không tìm thấy yêu cầu đặt thuê." });

        if (booking.TenantId != tenantId)
            return Forbid();

        if (booking.Status != BookingStatus.Pending)
            return Conflict(new { message = "Chỉ yêu cầu đang chờ mới được hủy." });

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(booking));
    }

    private static bool IsValidPaging(int page, int pageSize) =>
        page >= 1 && pageSize >= 1 && pageSize <= 50;

    private async Task<IActionResult> CreatePagedResponse(
        IQueryable<Booking> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // Đếm sau khi đã áp dụng quyền sở hữu và bộ lọc trạng thái.
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ToBookingResponseQuery(query)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PagedResponse<BookingResponse>(
            items,
            page,
            pageSize,
            totalCount,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages));
    }

    private IQueryable<BookingResponse> GetBookingResponseQuery() =>
        ToBookingResponseQuery(_db.Bookings.AsNoTracking());

    private static IQueryable<BookingResponse> ToBookingResponseQuery(IQueryable<Booking> query) =>
        query.Select(x => new BookingResponse(
            x.Id,
            x.PropertyId,
            x.Property.Title,
            x.TenantId,
            x.Tenant.FullName,
            x.Tenant.Email,
            x.StartDate,
            x.EndDate,
            x.NumberOfPeople,
            x.ProposedPrice,
            x.Message,
            x.Status,
            x.RespondedAt,
            x.RejectionReason,
            x.CreatedAt,
            x.UpdatedAt));

    private static BookingResponse ToResponse(Booking booking) => new(
        booking.Id,
        booking.PropertyId,
        booking.Property.Title,
        booking.TenantId,
        booking.Tenant.FullName,
        booking.Tenant.Email,
        booking.StartDate,
        booking.EndDate,
        booking.NumberOfPeople,
        booking.ProposedPrice,
        booking.Message,
        booking.Status,
        booking.RespondedAt,
        booking.RejectionReason,
        booking.CreatedAt,
        booking.UpdatedAt);

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
