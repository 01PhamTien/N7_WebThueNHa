using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Notifications;

namespace Web2_ThueNha.Services.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db) => _db = db;

    public async Task<PagedNotificationsResponse> GetNotificationsAsync(
        Guid userId,
        bool? isRead,
        NotificationType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
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

        return new PagedNotificationsResponse(
            items,
            page,
            pageSize,
            totalCount,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Notifications.CountAsync(
            x => x.UserId == userId && !x.IsRead,
            cancellationToken);

    public async Task<bool> MarkAsReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications.SingleOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == userId,
            cancellationToken);

        if (notification is null)
            return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<Guid> CreateAsync(
        Guid userId,
        string title,
        string content,
        NotificationType type,
        Guid? referenceId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId không hợp lệ.", nameof(userId));

        var normalizedTitle = title?.Trim() ?? string.Empty;
        var normalizedContent = content?.Trim() ?? string.Empty;

        if (normalizedTitle.Length is 0 or > 200)
            throw new ArgumentException("Tiêu đề thông báo phải từ 1 đến 200 ký tự.", nameof(title));

        if (normalizedContent.Length is 0 or > 2000)
            throw new ArgumentException("Nội dung thông báo phải từ 1 đến 2000 ký tự.", nameof(content));

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), "Loại thông báo không hợp lệ.");

        var notification = new Notification
        {
            UserId = userId,
            Title = normalizedTitle,
            Content = normalizedContent,
            NotificationType = type,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);
        return notification.Id;
    }

    public Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsRead, true),
                cancellationToken);
}
