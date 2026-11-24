using Web2_ThueNha.Models;
namespace Web2_ThueNha.Models.Chat;

public class MessageAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Message Message { get; set; } = null!;
}
