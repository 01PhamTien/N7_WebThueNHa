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

    public Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsRead, true),
                cancellationToken);
}
