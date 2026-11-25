using System.ComponentModel.DataAnnotations;
using Web2_ThueNha.Models;

namespace Web2_ThueNha.DTOs.Payments;

public sealed record InvoiceItemResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record PaymentResponse(
    Guid Id,
    Guid InvoiceId,
    Guid PayerId,
    decimal Amount,
    string PaymentMethod,
    string Status,
    string? PaymentProvider,
    string? TransactionCode,
    DateTime? PaidAt,
    string? FailureReason,
    Guid? ReviewedById,
    DateTime? ReviewedAt,
    DateTime CreatedAt);

public sealed record InvoiceListItem(
    Guid Id,
    string InvoiceCode,
    Guid ContractId,
    string ContractCode,
    Guid PropertyId,
    string PropertyTitle,
    Guid LandlordId,
    Guid TenantId,
    string InvoiceType,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt);

public sealed record PagedInvoicesResponse(
    IReadOnlyCollection<InvoiceListItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);

public sealed record InvoiceDetailResponse(
    Guid Id,
    Guid ContractId,
    string ContractCode,
    Guid PropertyId,
    string PropertyTitle,
    Guid LandlordId,
    Guid TenantId,
    string InvoiceCode,
    string InvoiceType,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt,
    IReadOnlyCollection<InvoiceItemResponse> Items,
    IReadOnlyCollection<PaymentResponse> Payments);

public sealed class CreateInvoiceRequest
{
    public InvoiceType InvoiceType { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }

    [Required]
    public List<CreateInvoiceItemRequest> Items { get; set; } = [];
}

public sealed class CreateInvoiceItemRequest
{
    [Required]
    [StringLength(300)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Quantity { get; set; } = 1;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal UnitPrice { get; set; }
}

public sealed class CreatePaymentRequest
{
    public PaymentMethod PaymentMethod { get; set; }

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }

    [StringLength(150)]
    public string? TransactionCode { get; set; }
}

public sealed class RejectPaymentRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}
