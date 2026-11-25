namespace Web2_ThueNha.Repositories.Properties;

public interface IPropertyQueryRepository
{
    IQueryable<Property> GetPublicPropertiesQuery();
    IQueryable<Property> GetLandlordPropertiesQuery(Guid landlordId);
    Task<Property?> GetDetailAsync(Guid propertyId, CancellationToken cancellationToken);
}
