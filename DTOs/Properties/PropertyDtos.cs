using System.ComponentModel.DataAnnotations;

namespace Web2_ThueNha.DTOs.Properties;

public sealed class PropertyCreateRequest
{
    [Range(1, int.MaxValue)]
    public int PropertyTypeId { get; set; }

    [Required, StringLength(200, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000000")]
    public decimal PricePerMonth { get; set; }

    [Range(typeof(decimal), "0", "1000000000000")]
    public decimal DepositAmount { get; set; }

    [Range(1, 100)]
    public int MaxOccupants { get; set; } = 2;

    [StringLength(1000)]
    public string? FurnishingDescription { get; set; }

    [StringLength(1000)]
    public string? AdditionalFeesDescription { get; set; }

    [Range(typeof(decimal), "0.01", "100000")]
    public decimal Area { get; set; }

    [Range(0, 50)]
    public int Bedrooms { get; set; }

    [Range(0, 50)]
    public int Bathrooms { get; set; }

    [Required, StringLength(300, MinimumLength = 5)]
    public string AddressLine { get; set; } = string.Empty;

    [StringLength(120)]
    public string? WardName { get; set; }

    [Required, StringLength(120)]
    public string ProvinceName { get; set; } = string.Empty;

    [Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; set; }
}

public sealed class PropertyUpdateRequest
{
    [Range(1, int.MaxValue)]
    public int PropertyTypeId { get; set; }

    [Required, StringLength(200, MinimumLength = 5)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000000")]
    public decimal PricePerMonth { get; set; }

    [Range(typeof(decimal), "0.01", "100000")]
    public decimal Area { get; set; }

    [Range(0, 50)]
    public int Bedrooms { get; set; }

    [Range(0, 50)]
    public int Bathrooms { get; set; }

    [Required, StringLength(300, MinimumLength = 5)]
    public string AddressLine { get; set; } = string.Empty;

    [StringLength(120)]
    public string? WardName { get; set; }

    [Required, StringLength(120)]
    public string ProvinceName { get; set; } = string.Empty;

    [Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; set; }
}

public sealed class PropertyStatusUpdateRequest
{
    public PropertyListingStatus Status { get; set; }
}

public sealed class PropertyImageCreateRequest
{
    [Required, Url, StringLength(1000)]
    public string ImageUrl { get; set; } = string.Empty;
}

public sealed record PropertyImageResponse(
    Guid Id,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder);

public sealed record PropertyResponse(
    Guid Id,
    Guid LandlordId,
    int PropertyTypeId,
    string PropertyTypeName,
    string Title,
    string? Description,
    decimal PricePerMonth,
    decimal DepositAmount,
    int MaxOccupants,
    string? FurnishingDescription,
    string? AdditionalFeesDescription,
    decimal Area,
    int Bedrooms,
    int Bathrooms,
    string AddressLine,
    string? WardName,
    string ProvinceName,
    decimal? Latitude,
    decimal? Longitude,
    string ModerationStatus,
    string ListingStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<PropertyImageResponse> Images);

public sealed record PagedPropertyResponse(
    IReadOnlyCollection<PropertyResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);

public sealed record PagedMyPropertiesResponse(
    IReadOnlyCollection<PropertyResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);
