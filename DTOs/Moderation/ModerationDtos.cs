using System.ComponentModel.DataAnnotations;
using Web2_ThueNha.Models;

namespace Web2_ThueNha.DTOs.Moderation;

public sealed class RejectPropertyRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ModeratePropertyRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

public sealed record ModerationPropertyListItem(
    Guid Id,
    string Title,
    string LandlordName,
    string ProvinceName,
    decimal PricePerMonth,
    PropertyModerationStatus ModerationStatus,
    PropertyListingStatus ListingStatus,
    DateTime CreatedAt);

public sealed record ModerationPropertyImageResponse(
    Guid Id,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder);

public sealed record ModerationPropertyLogResponse(
    Guid Id,
    string AdminName,
    ModerationAction Action,
    string? Reason,
    DateTime CreatedAt);

public sealed record ModerationPropertyDetailResponse(
    Guid Id,
    Guid LandlordId,
    string LandlordName,
    int PropertyTypeId,
    string PropertyTypeName,
    string Title,
    string? Description,
    decimal PricePerMonth,
    decimal Area,
    int Bedrooms,
    int Bathrooms,
    string AddressLine,
    string? WardName,
    string ProvinceName,
    decimal? Latitude,
    decimal? Longitude,
    PropertyModerationStatus ModerationStatus,
    PropertyListingStatus ListingStatus,
    DateTime CreatedAt,
    IReadOnlyCollection<ModerationPropertyImageResponse> Images,
    IReadOnlyCollection<string> Amenities,
    IReadOnlyCollection<ModerationPropertyLogResponse> ModerationLogs);

public sealed record PagedModerationPropertiesResponse(
    IReadOnlyCollection<ModerationPropertyListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);
