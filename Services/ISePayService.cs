namespace Web2_ThueNha.Services;

public sealed record SePayCheckout(
    Guid PaymentId,
    string OrderInvoiceNumber,
    string CheckoutUrl,
    IReadOnlyDictionary<string, string> FormFields);

public sealed class SePayRequestException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface ISePayService
{
    Task<SePayCheckout> CreateCheckoutAsync(
        Guid invoiceId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<bool> HandleGatewayIpnAsync(
        string rawBody,
        string? secretHeader,
        CancellationToken cancellationToken);

    Task<bool> HandleBankWebhookAsync(
        string rawBody,
        string? signature,
        string? timestamp,
        CancellationToken cancellationToken);
}
