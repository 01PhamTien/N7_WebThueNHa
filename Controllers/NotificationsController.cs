using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Notifications;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private const int MaxPageSize = 50;
    private readonly AppDbContext _db;

    public NotificationsController(AppDbContext db) => _db = db;

    /// <summary>Lấy danh sách thông báo của tài khoản hiện tại, có lọc và phân trang server-side.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedNotificationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? isRead,
        [FromQuery] NotificationType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize ||
            page > int.MaxValue / pageSize)
            return BadRequest(new { message = "page phải từ 1; pageSize phải từ 1 đến 50." });

        if (type.HasValue && !Enum.IsDefined(type.Value))
            return BadRequest(new { message = "Loại thông báo không hợp lệ." });

        IQueryable<Notification> query = _db.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (isRead.HasValue)
            query = query.Where(x => x.IsRead == isRead.Value);

        if (type.HasValue)
            query = query.Where(x => x.NotificationType == type.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = page > totalPages
            ? new List<NotificationItem>()
            : await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new NotificationItem(
                    x.Id,
                    x.Title,
                    x.Content,
                    x.NotificationType,
                    x.ReferenceId,
                    x.IsRead,
                    x.CreatedAt))
                .ToListAsync(cancellationToken);

        return Ok(new PagedNotificationsResponse(
            items,
            page,
            pageSize,
            totalCount,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages));
    }

    /// <summary>Đếm số thông báo chưa đọc của tài khoản hiện tại.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var count = await _db.Notifications
            .CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

        return Ok(new { unreadCount = count });
    }

    /// <summary>Đánh dấu một thông báo của tài khoản hiện tại là đã đọc.</summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var notification = await _db.Notifications.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);

        if (notification is null)
            return NotFound(new { message = "Không tìm thấy thông báo." });

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    /// <summary>Đánh dấu tất cả thông báo chưa đọc của tài khoản hiện tại là đã đọc.</summary>
    [HttpPatch("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var updatedCount = await _db.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsRead, true),
                cancellationToken);

        return Ok(new { updatedCount });
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
