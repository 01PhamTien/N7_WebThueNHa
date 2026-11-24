using Web2_ThueNha.Models;

namespace Web2_ThueNha.DTOs.Moderation;

public sealed record PagedModerationPropertiesResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasPrevious,
    bool HasNext,
    IReadOnlyCollection<ModerationPropertyListItem> Items);

public sealed record ModerationPropertyListItem(
    Guid Id,
    Guid LandlordId,
    string LandlordName,
    int PropertyTypeId,
    string PropertyTypeName,
    string Title,
    string ProvinceName,
    decimal PricePerMonth,
    DateTime CreatedAt,
    string ModerationStatus,
    string ListingStatus,
    string? PrimaryImageUrl);

public sealed record ModerationPropertyDetail(
    Guid Id,
    Guid LandlordId,
    string LandlordName,
    string LandlordEmail,
    string? LandlordPhone,
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
    string ModerationStatus,
    string ListingStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<ModerationImageItem> Images,
    IReadOnlyCollection<string> Amenities);

public sealed record ModerationImageItem(
    Guid Id,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder);

public sealed record ModerationLogItem(
    Guid Id,
    Guid PropertyId,
    Guid AdminId,
    string AdminName,
    string Action,
    string? Reason,
    DateTime CreatedAt);

public sealed record ModerationActionResponse(
    Guid PropertyId,
    string ModerationStatus,
    string ListingStatus,
    string Action,
    DateTime UpdatedAt);

public sealed record ModerationReasonRequest(
    string Reason);
