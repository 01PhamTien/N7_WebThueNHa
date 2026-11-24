using Web2_ThueNha.DTOs.Contracts;

namespace Web2_ThueNha.Services.Contracts;

public interface IContractService
{
    Task<ServiceResult<RentalContractResponse>> CreateFromBookingAsync(
        Guid bookingId, Guid userId, CancellationToken cancellationToken);

    Task<ServiceResult<RentalContractResponse>> UpdateAsync(
        Guid contractId, Guid userId, UpdateRentalContractRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<RentalContractResponse>> SubmitForSignatureAsync(
        Guid contractId, Guid userId, CancellationToken cancellationToken);

    Task<ServiceResult<RentalContractResponse>> SignAsync(
        Guid contractId, Guid userId, CancellationToken cancellationToken);
}
