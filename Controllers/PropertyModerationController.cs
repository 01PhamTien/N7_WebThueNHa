using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Moderation;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/admin/properties")]
[Authorize(Roles = "Admin")]
public sealed class PropertyModerationController(AppDbContext db) : ControllerBase
{
    private const int MaxPageSize = 50;

    /// <summary>Danh sách tin theo trạng thái kiểm duyệt, mặc định là Pending.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedModerationPropertiesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProperties(
        [FromQuery] PropertyModerationStatus status = PropertyModerationStatus.Pending,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize || !Enum.IsDefined(status))
            return BadRequest(new { message = "Tham số lọc hoặc phân trang không hợp lệ." });

        var query = db.Properties.AsNoTracking()
            .Where(x => x.ModerationStatus == status);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (int)Math.Min(((long)page - 1) * pageSize, int.MaxValue);

        var properties = await query
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new ModerationPropertyListItem(
                x.Id,
                x.Title,
                x.Landlord.FullName,
                x.ProvinceName,
                x.PricePerMonth,
                x.ModerationStatus,
                x.ListingStatus,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedModerationPropertiesResponse(
            properties,
            page,
            pageSize,
            totalCount,
            totalPages,
            page > 1,
            page < totalPages));
    }

    /// <summary>Chi tiết tin đăng, hình ảnh, tiện ích và lịch sử kiểm duyệt.</summary>
    [HttpGet("{propertyId:guid}")]
    [ProducesResponseType(typeof(ModerationPropertyDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid propertyId, CancellationToken cancellationToken)
    {
        var property = await db.Properties.AsNoTracking()
            .Include(x => x.Landlord)
            .Include(x => x.PropertyType)
            .Include(x => x.Images.OrderBy(image => image.DisplayOrder))
            .Include(x => x.PropertyAmenities)
                .ThenInclude(x => x.Amenity)
            .Include(x => x.ModerationLogs)
                .ThenInclude(x => x.Admin)
            .SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken);

        if (property is null)
            return NotFound(new { message = "Không tìm thấy tin đăng." });

        var response = new ModerationPropertyDetailResponse(
            property.Id,
            property.LandlordId,
            property.Landlord.FullName,
            property.PropertyTypeId,
            property.PropertyType.Name,
            property.Title,
            property.Description,
            property.PricePerMonth,
            property.Area,
            property.Bedrooms,
            property.Bathrooms,
            property.AddressLine,
            property.WardName,
            property.ProvinceName,
            property.Latitude,
            property.Longitude,
            property.ModerationStatus,
            property.ListingStatus,
            property.CreatedAt,
            property.Images.Select(image => new ModerationPropertyImageResponse(
                image.Id, image.ImageUrl, image.IsPrimary, image.DisplayOrder)).ToArray(),
            property.PropertyAmenities.Select(item => item.Amenity.Name).OrderBy(name => name).ToArray(),
            property.ModerationLogs
                .OrderByDescending(log => log.CreatedAt)
                .Select(log => new ModerationPropertyLogResponse(
                    log.Id, log.Admin.FullName, log.Action, log.Reason, log.CreatedAt))
                .ToArray());

        return Ok(response);
    }

    /// <summary>Duyệt tin đang chờ; tin được duyệt vẫn cần chủ nhà đăng công khai.</summary>
    [HttpPost("{propertyId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Approve(Guid propertyId, CancellationToken cancellationToken) =>
        ChangePendingStatus(propertyId, PropertyModerationStatus.Approved,
            ModerationAction.Approved, null, cancellationToken);

    /// <summary>Từ chối tin đang chờ và bắt buộc có lý do.</summary>
    [HttpPost("{propertyId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Reject(
        Guid propertyId,
        [FromBody] RejectPropertyRequest request,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason.Trim();
        if (reason.Length < 5)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Lý do từ chối phải có ít nhất 5 ký tự." }));

        return ChangePendingStatus(propertyId, PropertyModerationStatus.Rejected,
            ModerationAction.Rejected, reason, cancellationToken);
    }

    /// <summary>Ẩn tin đã duyệt và đang Published; bắt buộc ghi lý do.</summary>
    [HttpPost("{propertyId:guid}/hide")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Hide(
        Guid propertyId,
        [FromBody] ModeratePropertyRequest request,
        CancellationToken cancellationToken) =>
        ChangePublishedVisibility(propertyId, PropertyListingStatus.Hidden,
            ModerationAction.Hidden, request.Reason, cancellationToken);

    /// <summary>Khôi phục tin đã duyệt đang Hidden về Published; bắt buộc ghi lý do.</summary>
    [HttpPost("{propertyId:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Restore(
        Guid propertyId,
        [FromBody] ModeratePropertyRequest request,
        CancellationToken cancellationToken) =>
        ChangePublishedVisibility(propertyId, PropertyListingStatus.Published,
            ModerationAction.Restored, request.Reason, cancellationToken);

    private async Task<IActionResult> ChangePendingStatus(
        Guid propertyId,
        PropertyModerationStatus targetStatus,
        ModerationAction action,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminId))
            return Unauthorized();

        var property = await db.Properties
            .SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken);

        if (property is null)
            return NotFound(new { message = "Không tìm thấy tin đăng." });

        if (property.ModerationStatus != PropertyModerationStatus.Pending)
            return Conflict(new { message = "Chỉ tin đang chờ duyệt mới có thể được duyệt hoặc từ chối." });

        property.ModerationStatus = targetStatus;
        property.UpdatedAt = DateTime.UtcNow;

        if (targetStatus == PropertyModerationStatus.Rejected)
            property.ListingStatus = PropertyListingStatus.Draft;

        db.PropertyModerationLogs.Add(new PropertyModerationLog
        {
            PropertyId = property.Id,
            AdminId = adminId,
            Action = action,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = action == ModerationAction.Approved ? "Đã duyệt tin đăng." : "Đã từ chối tin đăng.", propertyId = property.Id, property.ModerationStatus, property.ListingStatus });
    }

    private async Task<IActionResult> ChangePublishedVisibility(
        Guid propertyId,
        PropertyListingStatus targetStatus,
        ModerationAction action,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminId))
            return Unauthorized();

        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length < 5)
            return BadRequest(new { message = "Lý do phải có ít nhất 5 ký tự." });

        var property = await db.Properties
            .SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken);

        if (property is null)
            return NotFound(new { message = "Không tìm thấy tin đăng." });

        var isValidTransition =
            property.ModerationStatus == PropertyModerationStatus.Approved &&
            ((action == ModerationAction.Hidden && property.ListingStatus == PropertyListingStatus.Published) ||
             (action == ModerationAction.Restored && property.ListingStatus == PropertyListingStatus.Hidden));

        if (!isValidTransition)
            return Conflict(new { message = "Trạng thái tin đăng hiện tại không cho phép thực hiện thao tác này." });

        property.ListingStatus = targetStatus;
        property.UpdatedAt = DateTime.UtcNow;

        db.PropertyModerationLogs.Add(new PropertyModerationLog
        {
            PropertyId = property.Id,
            AdminId = adminId,
            Action = action,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = action == ModerationAction.Hidden ? "Đã ẩn tin đăng." : "Đã khôi phục tin đăng.", propertyId = property.Id, property.ModerationStatus, property.ListingStatus });
    }

    private bool TryGetAdminId(out Guid adminId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out adminId);
}
