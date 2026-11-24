using Web2_ThueNha.Models.Identity;
using Web2_ThueNha.Models.Properties;

namespace Web2_ThueNha.Models.Interactions;

public class Favorite
{
    public Guid UserId { get; set; }
    public Guid PropertyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Property Property { get; set; } = null!;
}
