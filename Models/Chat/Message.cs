using Web2_ThueNha.Models.Identity;

namespace Web2_ThueNha.Models.Chat;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string? Content { get; set; }
    public MessageType MessageType { get; set; } = MessageType.Text;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}
