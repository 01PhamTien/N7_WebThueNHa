using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;

namespace Web2_ThueNha.Repositories.Favorites;

/// <summary>Truy vấn và thao tác lưu danh sách yêu thích bằng EF Core.</summary>
public sealed class FavoriteRepository(AppDbContext db) : IFavoriteRepository
{
    public IQueryable<Favorite> GetUserFavorites(Guid userId) =>
        db.Favorites.AsNoTracking().Where(x =>
            x.UserId == userId &&
            x.Property.ModerationStatus == PropertyModerationStatus.Approved &&
            x.Property.ListingStatus == PropertyListingStatus.Published);

    public Task<bool> IsPropertyPublicAsync(Guid propertyId, CancellationToken cancellationToken) =>
        db.Properties.AsNoTracking().AnyAsync(x =>
            x.Id == propertyId &&
            x.ModerationStatus == PropertyModerationStatus.Approved &&
            x.ListingStatus == PropertyListingStatus.Published, cancellationToken);

    public Task<bool> PropertyExistsAsync(Guid propertyId, CancellationToken cancellationToken) =>
        db.Properties.AsNoTracking().AnyAsync(x => x.Id == propertyId, cancellationToken);

    public Task<bool> ExistsAsync(Guid userId, Guid propertyId, CancellationToken cancellationToken) =>
        db.Favorites.AsNoTracking().AnyAsync(
            x => x.UserId == userId && x.PropertyId == propertyId, cancellationToken);

    public async Task<bool> AddAsync(Favorite favorite, CancellationToken cancellationToken)
    {
        if (await ExistsAsync(favorite.UserId, favorite.PropertyId, cancellationToken))
            return false;

        db.Favorites.Add(favorite);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            // Khóa chính kép (UserId, PropertyId) xử lý hai request đồng thời.
            db.Entry(favorite).State = EntityState.Detached;
            if (await ExistsAsync(favorite.UserId, favorite.PropertyId, cancellationToken))
                return false;

            throw;
        }
    }

    public async Task RemoveAsync(Guid userId, Guid propertyId, CancellationToken cancellationToken)
    {
        await db.Favorites
            .Where(x => x.UserId == userId && x.PropertyId == propertyId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
