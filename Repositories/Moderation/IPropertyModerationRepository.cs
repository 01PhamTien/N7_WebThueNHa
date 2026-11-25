namespace Web2_ThueNha.Repositories.Moderation;

public interface IPropertyModerationRepository
{
    IQueryable<Property> GetListQuery(
        PropertyModerationStatus status, string? keyword, string? province, int? propertyTypeId);
    Task<Property?> GetDetailAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<List<PropertyModerationLog>> GetLogsAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<Property?> GetForUpdateAsync(Guid propertyId, CancellationToken cancellationToken);
}
