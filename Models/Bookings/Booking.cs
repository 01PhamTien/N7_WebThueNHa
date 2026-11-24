using Web2_ThueNha.Models.Identity;
using Web2_ThueNha.Models.Properties;

namespace Web2_ThueNha.Models.Bookings;

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int NumberOfPeople { get; set; }
    public decimal ProposedPrice { get; set; }
    public string? Message { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime? RespondedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Property Property { get; set; } = null!;
    public User Tenant { get; set; } = null!;
    public RentalContract? RentalContract { get; set; }
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
