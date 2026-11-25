using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;

namespace Web2_ThueNha.Repositories.Moderation;

/// <summary>Đóng gói các truy vấn đọc và truy vấn cập nhật của luồng kiểm duyệt.</summary>
public sealed class PropertyModerationRepository(AppDbContext db) : IPropertyModerationRepository
{
    public IQueryable<Property> GetListQuery(
        PropertyModerationStatus status, string? keyword, string? province, int? propertyTypeId)
    {
        IQueryable<Property> query = db.Properties.AsNoTracking()
            .Where(x => x.ModerationStatus == status);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x =>
                x.Title.Contains(term) ||
                x.AddressLine.Contains(term) ||
                (x.WardName != null && x.WardName.Contains(term)) ||
                x.Landlord.FullName.Contains(term) ||
                x.Landlord.Email.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(province))
        {
            var provinceName = province.Trim();
            query = query.Where(x => x.ProvinceName.Contains(provinceName));
        }

        if (propertyTypeId.HasValue)
            query = query.Where(x => x.PropertyTypeId == propertyTypeId.Value);

        return query;
    }

    public Task<Property?> GetDetailAsync(Guid propertyId, CancellationToken cancellationToken) =>
        db.Properties.AsNoTracking()
            .Include(x => x.Landlord)
            .Include(x => x.PropertyType)
            .Include(x => x.Images.OrderBy(image => image.DisplayOrder))
            .Include(x => x.PropertyAmenities)
                .ThenInclude(x => x.Amenity)
            .SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken);

    public Task<List<PropertyModerationLog>> GetLogsAsync(
        Guid propertyId, CancellationToken cancellationToken) =>
        db.PropertyModerationLogs.AsNoTracking()
            .Where(log => log.PropertyId == propertyId)
            .Include(log => log.Admin)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .ToListAsync(cancellationToken);

    public Task<Property?> GetForUpdateAsync(Guid propertyId, CancellationToken cancellationToken) =>
        db.Properties.SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken);
}
