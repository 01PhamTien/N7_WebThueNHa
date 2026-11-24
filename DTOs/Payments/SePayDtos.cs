using System.ComponentModel.DataAnnotations;

namespace Web2_ThueNha.DTOs.Payments;

public sealed class CreateSePayCheckoutRequest
{
    [Range(typeof(decimal), "1", "9999999999999999")]
    public decimal Amount { get; set; }
}

public sealed record SePayCheckoutResponse(
    Guid PaymentId,
    string InvoiceNumber,
    string CheckoutUrl,
    IReadOnlyDictionary<string, string> FormFields);
