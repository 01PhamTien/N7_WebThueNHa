namespace Web2_ThueNha.DTOs.Admin;

public sealed record AdminPaymentListItem(
    Guid Id,
    Guid InvoiceId,
    string InvoiceCode,
    string ContractCode,
    string PropertyTitle,
    Guid PayerId,
    string PayerName,
    decimal Amount,
    string PaymentMethod,
    string Status,
    string? PaymentProvider,
    string? TransactionCode,
    DateTime? PaidAt,
    string? FailureReason,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    DateTime CreatedAt);

public sealed record AdminReviewListItem(
    Guid Id,
    Guid ContractId,
    string ContractCode,
    string PropertyTitle,
    string TenantName,
    int Rating,
    string? Comment,
    DateTime CreatedAt);

public sealed record AdminModerationLogListItem(
    Guid Id,
    Guid PropertyId,
    string PropertyTitle,
    string AdminName,
    string Action,
    string? Reason,
    DateTime CreatedAt);
