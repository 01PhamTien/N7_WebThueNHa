namespace Web2_ThueNha.DTOs.Favorites;

public sealed record FavoritePropertyItem(
    Guid PropertyId,
    string Title,
    string PropertyTypeName,
    decimal PricePerMonth,
    decimal Area,
    int Bedrooms,
    int Bathrooms,
    string AddressLine,
    string? WardName,
    string ProvinceName,
    string? PrimaryImageUrl,
    DateTime FavoritedAt);

public sealed record PagedFavoritesResponse(
    IReadOnlyCollection<FavoritePropertyItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasPrevious,
    bool HasNext);

public sealed record FavoriteStatusResponse(
    Guid PropertyId,
    bool IsFavorite);
