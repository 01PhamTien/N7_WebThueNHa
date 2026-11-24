namespace Web2_ThueNha.Models;

public sealed class PaymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? CustomerId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }
}

public sealed class SePayWebhookEvent
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
