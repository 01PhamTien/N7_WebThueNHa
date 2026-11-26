using Web2_ThueNha.Models.Identity;

namespace Web2_ThueNha.Models.Payments;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long SequenceNumber { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid PayerId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? PaymentProvider { get; set; }
    public string? TransactionCode { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Invoice Invoice { get; set; } = null!;
    public User Payer { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
