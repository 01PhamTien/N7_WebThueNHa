using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Identity;
using Web2_ThueNha.Models.Properties;

namespace Web2_ThueNha.Models.Moderation;

public class PropertyModerationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Guid AdminId { get; set; }
    public ModerationAction Action { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Property Property { get; set; } = null!;
    public User Admin { get; set; } = null!;
}
