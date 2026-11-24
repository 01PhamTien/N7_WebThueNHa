using System.ComponentModel.DataAnnotations;

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

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal ProposedPrice { get; set; }

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
    decimal ProposedPrice,
    string? Message,
    BookingStatus Status,
    DateTime? RespondedAt,
    string? RejectionReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt);


public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);
