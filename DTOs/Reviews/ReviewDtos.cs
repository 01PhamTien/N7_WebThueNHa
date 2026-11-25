using System.ComponentModel.DataAnnotations;

namespace Web2_ThueNha.DTOs.Reviews;

public sealed class CreateReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }
}

public sealed record PropertyReviewItem(
    Guid Id,
    int Rating,
    string? Comment,
    DateTime CreatedAt);

public sealed record MyReviewItem(
    Guid Id,
    Guid ContractId,
    Guid PropertyId,
    string PropertyTitle,
    int Rating,
    string? Comment,
    DateTime CreatedAt);

public sealed record PropertyReviewsResponse(
    double AverageRating,
    int TotalReviews,
    IReadOnlyList<PropertyReviewItem> Items);
