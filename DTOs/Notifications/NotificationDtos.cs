namespace Web2_ThueNha.DTOs.Notifications;

public sealed record NotificationItem(
    Guid Id,
    string Title,
    string Content,
    NotificationType NotificationType,
    Guid? ReferenceId,
    bool IsRead,
    DateTime CreatedAt);

public sealed record PagedNotificationsResponse(
    IReadOnlyCollection<NotificationItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);
