using Microsoft.EntityFrameworkCore;

namespace Web2_ThueNha.Repositories.Favorites;

public interface IFavoriteRepository
{
    IQueryable<Favorite> GetUserFavorites(Guid userId);
    Task<bool> IsPropertyPublicAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<bool> PropertyExistsAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid userId, Guid propertyId, CancellationToken cancellationToken);
    Task<bool> AddAsync(Favorite favorite, CancellationToken cancellationToken);
    Task RemoveAsync(Guid userId, Guid propertyId, CancellationToken cancellationToken);
}
