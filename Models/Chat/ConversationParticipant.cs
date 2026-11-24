using Web2_ThueNha.Models.Identity;

namespace Web2_ThueNha.Models.Chat;

public class ConversationParticipant
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadAt { get; set; }
    public bool IsArchived { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
}
