using Web2_ThueNha.Models;

namespace Web2_ThueNha.Services;

public sealed record CreateSePayPaymentRequest(
    decimal Amount,
    string Description,
    string? CustomerId);

public sealed record SePayCheckout(
    string InvoiceNumber,
    string CheckoutUrl,
    IReadOnlyDictionary<string, string> FormFields);

public interface ISePayService
{
    Task<SePayCheckout> CreateCheckoutAsync(CreateSePayPaymentRequest request, CancellationToken cancellationToken);
    Task<bool> HandleGatewayIpnAsync(string rawBody, string? secretHeader, CancellationToken cancellationToken);
    Task<bool> HandleBankWebhookAsync(string rawBody, string? signature, string? timestamp, CancellationToken cancellationToken);
}
