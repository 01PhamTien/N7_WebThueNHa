using Web2_ThueNha.Models.Identity;
using Web2_ThueNha.Models.Properties;
using Web2_ThueNha.Models.Bookings;

namespace Web2_ThueNha.Models.Contracts;

public class RentalContract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid LandlordId { get; set; }
    public Guid TenantId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public int PaymentDueDay { get; set; } = 1;
    public string? Terms { get; set; }
    public string? ContractUrl { get; set; }
    public RentalContractStatus Status { get; set; } = RentalContractStatus.Draft;
    public DateTime? SignedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Booking Booking { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public User Landlord { get; set; } = null!;
    public User Tenant { get; set; } = null!;
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public Review? Review { get; set; }
}
