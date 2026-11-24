using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web2_ThueNha.DTOs.Notifications;
using Web2_ThueNha.Services.Notifications;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private const int MaxPageSize = 50;
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService) =>
        _notificationService = notificationService;

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

        var result = await _notificationService.GetNotificationsAsync(
            userId, isRead, type, page, pageSize, cancellationToken);

        return Ok(result);
    }

    /// <summary>Đếm số thông báo chưa đọc của tài khoản hiện tại.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var count = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
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

        var updated = await _notificationService.MarkAsReadAsync(id, userId, cancellationToken);
        return updated
            ? NoContent()
            : NotFound(new { message = "Không tìm thấy thông báo." });
    }

    /// <summary>Đánh dấu tất cả thông báo chưa đọc của tài khoản hiện tại là đã đọc.</summary>
    [HttpPatch("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var updatedCount = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(new { updatedCount });
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
