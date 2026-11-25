using Web2_ThueNha.Services;
using Web2_ThueNha.DTOs.Payments;
using Web2_ThueNha.Models.Payments;

namespace Web2_ThueNha.Services.Payments;

public interface IPaymentService
{
    Task<ServiceResult<InvoiceDetailResponse>> CreateInvoiceAsync(
        Guid contractId, Guid userId, CreateInvoiceRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<PaymentResponse>> CreatePaymentAsync(
        Guid invoiceId, Guid userId, CreatePaymentRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<PaymentResponse>> ConfirmPaymentAsync(
        Guid paymentId, Guid userId, bool isAdmin, CancellationToken cancellationToken);

    Task<ServiceResult<PaymentResponse>> RejectPaymentAsync(
        Guid paymentId, Guid userId, bool isAdmin, RejectPaymentRequest request, CancellationToken cancellationToken);

    Task EnsureScheduledInvoicesAsync(Guid userId, bool isLandlord, bool isTenant, CancellationToken cancellationToken);

    Task MarkOverdueInvoicesAsync(Guid userId, bool isLandlord, bool isTenant, CancellationToken cancellationToken);

    Task RefreshOverdueStatusAsync(Invoice invoice, CancellationToken cancellationToken);
}
