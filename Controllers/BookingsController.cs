using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Bookings;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Contracts;
using Web2_ThueNha.Services.Notifications;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public sealed class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;

    public BookingsController(AppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    /// <summary>Người thuê gửi yêu cầu đặt thuê cho một căn nhà đang được đăng.</summary>
    [HttpPost]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var tenantId))
            return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (request.StartDate < today)
            return BadRequest(new { message = "Ngày bắt đầu không được ở trong quá khứ." });

        if (request.EndDate <= request.StartDate)
            return BadRequest(new
            {
                message = "Ngày kết thúc phải sau ngày bắt đầu."
            });

        var property = await _db.Properties
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.PropertyId, cancellationToken);

        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });

        if (request.NumberOfPeople > property.MaxOccupants)
            return BadRequest(new { message = $"Căn nhà chỉ cho phép tối đa {property.MaxOccupants} người ở." });

        if (property.LandlordId == tenantId)
            return BadRequest(new { message = "Chủ nhà không thể tự gửi yêu cầu thuê căn nhà của mình." });

        if (property.ModerationStatus != PropertyModerationStatus.Approved ||
            property.ListingStatus != PropertyListingStatus.Published)
            return Conflict(new { message = "Căn nhà hiện không ở trạng thái được phép nhận yêu cầu thuê." });

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        // Mỗi khách chỉ được có một yêu cầu đang chờ xử lý cho cùng một căn nhà.
        // Chặn cả trường hợp bấm gửi liên tục hoặc gửi lại cùng căn nhà khi yêu cầu cũ chưa được xử lý.
        var hasDuplicatePending = await _db.Bookings.AnyAsync(x =>
            x.PropertyId == request.PropertyId &&
            x.TenantId == tenantId &&
            x.Status == BookingStatus.Pending, cancellationToken);

        if (hasDuplicatePending)
            return Conflict(new { message = "Bạn đã gửi yêu cầu thuê căn nhà này và đang chờ chủ nhà phản hồi. Vui lòng kiểm tra mục Đơn thuê của tôi, không cần gửi lại." });

        var hasAcceptedConflict = await _db.Bookings.AnyAsync(x =>
            x.PropertyId == request.PropertyId &&
            x.Status == BookingStatus.Accepted &&
            x.StartDate < request.EndDate &&
            x.EndDate > request.StartDate, cancellationToken);

        var hasContractConflict = await _db.RentalContracts.AnyAsync(x =>
            x.PropertyId == request.PropertyId &&
            x.Status != RentalContractStatus.Completed &&
            x.Status != RentalContractStatus.Terminated &&
            x.Status != RentalContractStatus.Cancelled &&
            x.StartDate < request.EndDate &&
            x.EndDate > request.StartDate,
            cancellationToken);

        if (hasAcceptedConflict || hasContractConflict)
            return Conflict(new { message = "Khoảng thời gian này đã có yêu cầu thuê được chấp nhận hoặc hợp đồng khác." });

        var booking = new Booking
        {
            PropertyId = property.Id,
            TenantId = tenantId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfPeople = request.NumberOfPeople,
            ProposedPrice = property.PricePerMonth,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(cancellationToken);
        await _notifications.CreateAsync(
            property.LandlordId,
            "Yêu cầu thuê mới",
            $"Bạn có yêu cầu thuê mới cho căn nhà {property.Title}.",
            NotificationType.Booking,
            booking.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Lọc trên entity trước khi project sang record DTO để EF Core dịch WHERE thành SQL.
        var response = await ToBookingResponseQuery(
                _db.Bookings.AsNoTracking().Where(x => x.Id == booking.Id))
            .SingleAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, response);
    }

    /// <summary>Người thuê xem các yêu cầu đặt thuê do mình tạo.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(PagedResponse<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIncoming(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] BookingStatus? status = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var landlordId))
            return Unauthorized();

        if (!IsValidPaging(page, pageSize))
            return BadRequest(new { message = "page phải lớn hơn hoặc bằng 1; pageSize phải từ 1 đến 50." });

        if ((status.HasValue && !Enum.IsDefined(status.Value)) ||
            (keyword is { Length: > 100 }))
            return BadRequest(new { message = "Tham số tìm kiếm hoặc trạng thái yêu cầu thuê không hợp lệ." });

        var query = _db.Bookings.AsNoTracking()
            .Where(x => x.Property.LandlordId == landlordId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x =>
                x.Tenant.FullName.Contains(term) ||
                x.Tenant.Email.Contains(term) ||
                x.Property.Title.Contains(term));
        }

        return await CreatePagedResponse(query, page, pageSize, cancellationToken);
    }

    /// <summary>Xem chi tiết yêu cầu nếu là người thuê tạo yêu cầu hoặc chủ nhà sở hữu căn nhà.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        // Không lọc trên BookingResponse sau projection; EF Core không dịch được
        // biểu thức khởi tạo record trong WHERE ở một số phiên bản/provider.
        var booking = await ToBookingResponseQuery(
                _db.Bookings.AsNoTracking().Where(x => x.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

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

        if (booking.NumberOfPeople > booking.Property.MaxOccupants)
            return Conflict(new { message = $"Số người trong yêu cầu vượt sức chứa hiện tại ({booking.Property.MaxOccupants} người). Vui lòng trao đổi lại với khách thuê." });

        if (booking.Property.ModerationStatus != PropertyModerationStatus.Approved ||
            booking.Property.ListingStatus != PropertyListingStatus.Published)
            return Conflict(new { message = "Căn nhà không còn ở trạng thái được phép cho thuê." });

        var hasAcceptedConflict = await _db.Bookings.AnyAsync(x =>
            x.Id != booking.Id &&
            x.PropertyId == booking.PropertyId &&
            x.Status == BookingStatus.Accepted &&
            x.StartDate < booking.EndDate &&
            x.EndDate > booking.StartDate, cancellationToken);

        var hasContractConflict = await _db.RentalContracts.AnyAsync(x =>
            x.BookingId != booking.Id &&
            x.PropertyId == booking.PropertyId &&
            x.Status != RentalContractStatus.Completed &&
            x.Status != RentalContractStatus.Terminated &&
            x.Status != RentalContractStatus.Cancelled &&
            x.StartDate < booking.EndDate &&
            x.EndDate > booking.StartDate,
            cancellationToken);

        if (hasAcceptedConflict || hasContractConflict)
            return Conflict(new { message = "Không thể chấp nhận vì thời hạn bị trùng với yêu cầu thuê đã chấp nhận hoặc hợp đồng khác." });

        booking.Status = BookingStatus.Accepted;
        booking.RespondedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        // Tạo hợp đồng cùng transaction chấp nhận yêu cầu; không ký hoặc thu tiền tự động.
        var now = DateTime.UtcNow;
        if (!await _db.RentalContracts.AnyAsync(x => x.BookingId == booking.Id, cancellationToken))
        {
            _db.RentalContracts.Add(new RentalContract
            {
                BookingId = booking.Id,
                PropertyId = booking.PropertyId,
                LandlordId = landlordId,
                TenantId = booking.TenantId,
                ContractCode = $"HD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                StartDate = booking.StartDate,
                EndDate = booking.EndDate,
                MonthlyRent = booking.ProposedPrice,
                DepositAmount = booking.Property.DepositAmount,
                PaymentDueDay = 1,
                Status = RentalContractStatus.Draft,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _notifications.CreateAsync(
            booking.TenantId,
            "Yêu cầu thuê đã được chấp nhận",
            $"Chủ nhà đã chấp nhận yêu cầu thuê căn nhà {booking.Property.Title}. Hợp đồng đang được chuẩn bị để hai bên ký.",
            NotificationType.Booking,
            booking.Id,
            cancellationToken);
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

        // Dùng Serializable để hai thao tác Accept/Reject đồng thời không cùng chuyển một booking.
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
            return Conflict(new { message = "Chỉ yêu cầu đang chờ mới được từ chối." });

        booking.Status = BookingStatus.Rejected;
        booking.RejectionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        booking.RespondedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _notifications.CreateAsync(
            booking.TenantId,
            "Yêu cầu thuê đã bị từ chối",
            $"Chủ nhà đã từ chối yêu cầu thuê căn nhà {booking.Property.Title}.",
            NotificationType.Booking,
            booking.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

        // Đảm bảo Cancel không đua với thao tác Accept/Reject trên cùng yêu cầu.
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

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
        await _notifications.CreateAsync(
            booking.Property.LandlordId,
            "Yêu cầu thuê đã bị hủy",
            $"Người thuê đã hủy yêu cầu thuê căn nhà {booking.Property.Title}.",
            NotificationType.Booking,
            booking.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(ToResponse(booking));
    }

    private static bool IsValidPaging(int page, int pageSize) =>
        page >= 1 &&
        pageSize >= 1 &&
        pageSize <= 50 &&
        page <= int.MaxValue / pageSize;

    private async Task<IActionResult> CreatePagedResponse(
        IQueryable<Booking> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // Đếm sau khi đã áp dụng quyền sở hữu và bộ lọc trạng thái.
        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Trang vượt tổng số trang trả danh sách rỗng, không tính OFFSET có thể tràn số.
        var items = page > totalPages
            ? new List<BookingResponse>()
            : await ToBookingResponseQuery(
                    query
                        .OrderByDescending(x => x.CreatedAt)
                        .ThenByDescending(x => x.Id)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize))
                .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<BookingResponse>(
            items,
            page,
            pageSize,
            totalCount,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages));
    }

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
