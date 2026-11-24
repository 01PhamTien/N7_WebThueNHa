using System.ComponentModel.DataAnnotations;

namespace Web2_ThueNha.DTOs.Chat;

public sealed class SendMessageRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}

public sealed class ArchiveConversationRequest
{
    public bool IsArchived { get; set; }
}

public sealed record ConversationListItem(
    Guid ConversationId,
    Guid PropertyId,
    string PropertyTitle,
    Guid TenantId,
    string TenantName,
    Guid LandlordId,
    string LandlordName,
    string? LastMessageContent,
    DateTime? LastMessageAt,
    int UnreadCount,
    DateTime CreatedAt,
    bool IsArchived);

public sealed record ChatAttachmentItem(
    Guid AttachmentId,
    string FileName,
    string? ContentType,
    long FileSize,
    string DownloadUrl);

public sealed record ChatMessageItem(
    Guid MessageId,
    Guid SenderId,
    string SenderName,
    string? Content,
    string MessageType,
    DateTime SentAt,
    DateTime? EditedAt,
    bool IsMine,
    IReadOnlyCollection<ChatAttachmentItem> Attachments);

public sealed record PagedConversations(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasPrevious,
    bool HasNext,
    IReadOnlyCollection<ConversationListItem> Items);

public sealed record PagedChatMessages(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasPrevious,
    bool HasNext,
    IReadOnlyCollection<ChatMessageItem> Items);

public sealed class EditMessageRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
