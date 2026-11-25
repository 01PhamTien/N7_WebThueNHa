using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Contracts;

namespace Web2_ThueNha.Models.Interactions;

public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public RentalContract Contract { get; set; } = null!;
}
