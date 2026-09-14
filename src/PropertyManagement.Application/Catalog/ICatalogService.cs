using PropertyManagement.Application.Catalog.Models;
using PropertyManagement.Application.Common.Models;

namespace PropertyManagement.Application.Catalog;

/// <summary>Property, unit and unit-type management plus the public unit catalogue.</summary>
public interface ICatalogService
{
    Task<List<PropertySummary>> GetPropertiesAsync(CancellationToken ct = default);
    Task<PropertyDetail?> GetPropertyAsync(int id, CancellationToken ct = default);
    Task<int> CreatePropertyAsync(PropertyInput input, CancellationToken ct = default);
    Task UpdatePropertyAsync(PropertyInput input, CancellationToken ct = default);
    Task DeletePropertyAsync(int id, CancellationToken ct = default);

    Task<List<UnitRow>> GetUnitsForPropertyAsync(int propertyId, CancellationToken ct = default);
    Task<UnitDetail?> GetUnitAsync(int id, CancellationToken ct = default);
    Task<List<LookupItem>> GetUnitTypeOptionsAsync(int? currentUnitTypeId, CancellationToken ct = default);
    Task<int> CreateUnitAsync(UnitInput input, CancellationToken ct = default);
    Task UpdateUnitAsync(UnitInput input, CancellationToken ct = default);
    Task DeleteUnitAsync(int id, CancellationToken ct = default);

    Task<UnitBrowseResult> BrowseUnitsAsync(UnitBrowseFilter filter, string? applicantId, CancellationToken ct = default);
}
