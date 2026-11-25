using System.ComponentModel.DataAnnotations;
using Web2_ThueNha.Models;

namespace Web2_ThueNha.DTOs.Bookings;

public sealed class CreateBookingRequest
{
    [Required]
    public Guid PropertyId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Range(1, 100)]
    public int NumberOfPeople { get; set; }

    [StringLength(1000)]
    public string? Message { get; set; }
}

public sealed class RejectBookingRequest
{
    [StringLength(1000)]
    public string? Reason { get; set; }
}

public sealed record BookingResponse(
    Guid Id,
    Guid PropertyId,
    string PropertyTitle,
    Guid TenantId,
    string TenantName,
    string TenantEmail,
    DateOnly StartDate,
    DateOnly EndDate,
    int NumberOfPeople,
    decimal MonthlyRent,
    string? Message,
    BookingStatus Status,
    DateTime? RespondedAt,
    string? RejectionReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public decimal EstimatedRent =>
        RentalPricing.Calculate(MonthlyRent, StartDate, EndDate).TotalAmount;

    public int FullMonths =>
        RentalPricing.Calculate(MonthlyRent, StartDate, EndDate).FullMonths;

    public int ExtraDays =>
        RentalPricing.Calculate(ProposedPrice, StartDate, EndDate).ExtraDays;
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);
