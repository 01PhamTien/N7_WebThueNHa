using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Identity;
using Web2_ThueNha.Models.Properties;
using Web2_ThueNha.Models.Bookings;

namespace Web2_ThueNha.Models.Chat;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }
    public Guid? BookingId { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public User Tenant { get; set; } = null!;
    public User Landlord { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public Booking? Booking { get; set; }
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
