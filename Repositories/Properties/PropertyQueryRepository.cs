using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;

namespace Web2_ThueNha.Repositories.Properties;

/// <summary>Các truy vấn đọc dùng chung của API quản lý căn nhà.</summary>
public sealed class PropertyQueryRepository(AppDbContext db) : IPropertyQueryRepository
{
    public IQueryable<Property> GetPublicPropertiesQuery() =>
        db.Properties.AsNoTracking()
            .Where(x =>
                x.ListingStatus == PropertyListingStatus.Published &&
                x.ModerationStatus == PropertyModerationStatus.Approved);

    public IQueryable<Property> GetLandlordPropertiesQuery(Guid landlordId) =>
        db.Properties.AsNoTracking().Where(x => x.LandlordId == landlordId);

    public Task<Property?> GetDetailAsync(Guid propertyId, CancellationToken cancellationToken) =>
        db.Properties.AsNoTracking()
            .Include(x => x.PropertyType)
            .Include(x => x.Images.OrderBy(image => image.DisplayOrder))
            .SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken);
}
