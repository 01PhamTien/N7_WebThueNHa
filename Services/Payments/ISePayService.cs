using Web2_ThueNha.DTOs.Payments;

namespace Web2_ThueNha.Services.Payments;

public interface ISePayService
{
    Task<SePayCheckoutResponse> CreateCheckoutAsync(
        Guid invoiceId,
        Guid tenantId,
        decimal amount,
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
