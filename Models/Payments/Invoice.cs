using Web2_ThueNha.Models.Contracts;

namespace Web2_ThueNha.Models.Payments;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long SequenceNumber { get; set; }
    public Guid ContractId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public InvoiceType InvoiceType { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public RentalContract Contract { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
