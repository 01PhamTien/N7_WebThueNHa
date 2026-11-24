using System.ComponentModel.DataAnnotations;

namespace Web2_ThueNha.DTOs.Contracts;

public sealed record ContractListItem(
    Guid Id,
    string ContractCode,
    Guid BookingId,
    Guid PropertyId,
    string PropertyTitle,
    Guid LandlordId,
    string LandlordName,
    Guid TenantId,
    string TenantName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    string Status,
    DateTime CreatedAt);

public sealed record PagedContractsResponse(
    IReadOnlyCollection<ContractListItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);

public sealed record RentalContractResponse(
    Guid Id,
    Guid BookingId,
    Guid PropertyId,
    Guid LandlordId,
    Guid TenantId,
    string ContractCode,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    int PaymentDueDay,
    string? Terms,
    string? ContractUrl,
    string Status,
    DateTime? LandlordSignedAt,
    DateTime? TenantSignedAt,
    DateTime? SignedAt,
    DateTime CreatedAt);

public sealed record RentalContractDetailResponse(
    RentalContractResponse Contract,
    string PropertyTitle,
    string LandlordName,
    string TenantName);

public sealed class UpdateRentalContractRequest
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal MonthlyRent { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal DepositAmount { get; set; }

    [Range(1, 28)]
    public int PaymentDueDay { get; set; } = 1;

    [StringLength(4000)]
    public string? Terms { get; set; }
}
