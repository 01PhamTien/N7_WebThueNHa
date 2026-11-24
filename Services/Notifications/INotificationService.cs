using Web2_ThueNha.DTOs.Notifications;

namespace Web2_ThueNha.Services.Notifications;

public interface INotificationService
{
    Task<PagedNotificationsResponse> GetNotificationsAsync(
        Guid userId,
        bool? isRead,
        NotificationType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken);

    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken);
}
