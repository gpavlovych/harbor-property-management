using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Catalog.Models;
using PropertyManagement.Application.Common.Interfaces;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Application.Catalog;

public class CatalogService(IApplicationDbContext db, TimeProvider time) : ICatalogService
{
    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    public Task<List<PropertySummary>> GetPropertiesAsync(CancellationToken ct = default)
    {
        var today = Today;
        return db.Properties
            .OrderBy(p => p.Name)
            .Select(p => new PropertySummary
            {
                Id = p.Id,
                Name = p.Name,
                Address = p.StreetAddress + ", " + p.City + ", " + p.State + " " + p.PostalCode,
                UnitCount = p.Units.Count,
                AvailableUnitCount = p.Units.Count(u => !u.Leases.Any(l => l.StartDate <= today && l.EndDate >= today))
            })
            .ToListAsync(ct);
    }

    public Task<PropertyDetail?> GetPropertyAsync(int id, CancellationToken ct = default) =>
        db.Properties
            .Where(p => p.Id == id)
            .Select(p => new PropertyDetail { Id = p.Id, Name = p.Name, StreetAddress = p.StreetAddress, City = p.City, State = p.State, PostalCode = p.PostalCode })
            .FirstOrDefaultAsync(ct);

    public async Task<int> CreatePropertyAsync(PropertyInput input, CancellationToken ct = default)
    {
        var property = new Property { CreatedAt = time.GetUtcNow().UtcDateTime };
        Apply(input, property);
        db.Properties.Add(property);
        await db.SaveChangesAsync(ct);
        return property.Id;
    }

    public async Task UpdatePropertyAsync(PropertyInput input, CancellationToken ct = default)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == input.Id, ct)
            ?? throw new EntityNotFoundException("Property not found.");
        Apply(input, property);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeletePropertyAsync(int id, CancellationToken ct = default)
    {
        var property = await db.Properties.Include(p => p.Units).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new EntityNotFoundException("Property not found.");

        var unitIds = property.Units.Select(u => u.Id).ToList();
        if (await db.Leases.AnyAsync(l => unitIds.Contains(l.UnitId), ct))
            throw new DomainException("This property cannot be removed because one or more of its units has a lease.");
        if (await db.RentalApplications.AnyAsync(a => unitIds.Contains(a.UnitId), ct))
            throw new DomainException("This property cannot be removed because one or more of its units has rental applications.");

        db.Properties.Remove(property);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<UnitRow>> GetUnitsForPropertyAsync(int propertyId, CancellationToken ct = default)
    {
        var today = Today;
        return db.Units
            .Where(u => u.PropertyId == propertyId)
            .OrderBy(u => u.UnitNumber)
            .Select(u => new UnitRow
            {
                Id = u.Id,
                PropertyId = u.PropertyId,
                UnitNumber = u.UnitNumber,
                UnitType = u.UnitType.Name,
                UnitTypeIsActive = u.UnitType.IsActive,
                Bedrooms = u.Bedrooms,
                MonthlyRent = u.MonthlyRent,
                IsAvailable = !u.Leases.Any(l => l.StartDate <= today && l.EndDate >= today),
                SubmittedApplicationCount = u.Applications.Count(a => a.Status == ApplicationStatus.Submitted)
            })
            .ToListAsync(ct);
    }

    public Task<UnitDetail?> GetUnitAsync(int id, CancellationToken ct = default) =>
        db.Units
            .Where(u => u.Id == id)
            .Select(u => new UnitDetail
            {
                Id = u.Id, PropertyId = u.PropertyId, PropertyName = u.Property.Name, UnitNumber = u.UnitNumber,
                Bedrooms = u.Bedrooms, MonthlyRent = u.MonthlyRent, UnitTypeId = u.UnitTypeId
            })
            .FirstOrDefaultAsync(ct);

    /// <summary>Active types are always offered. An inactive type is offered only when it is the unit's current type.</summary>
    public Task<List<LookupItem>> GetUnitTypeOptionsAsync(int? currentUnitTypeId, CancellationToken ct = default) =>
        db.UnitTypes
            .Where(t => t.IsActive || t.Id == currentUnitTypeId)
            .OrderBy(t => t.SortOrder)
            .Select(t => new LookupItem { Id = t.Id, Name = t.IsActive ? t.Name : t.Name + " (inactive)" })
            .ToListAsync(ct);

    public async Task<int> CreateUnitAsync(UnitInput input, CancellationToken ct = default)
    {
        if (!await db.Properties.AnyAsync(p => p.Id == input.PropertyId, ct))
            throw new EntityNotFoundException("Property not found.");

        await EnsureUnitTypeSelectableAsync(input.UnitTypeId, currentUnitTypeId: null, ct);
        await EnsureUnitNumberUniqueAsync(input, ct);

        var unit = new Unit { PropertyId = input.PropertyId, CreatedAt = time.GetUtcNow().UtcDateTime };
        Apply(input, unit);
        db.Units.Add(unit);
        await db.SaveChangesAsync(ct);
        return unit.Id;
    }

    public async Task UpdateUnitAsync(UnitInput input, CancellationToken ct = default)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == input.Id, ct)
            ?? throw new EntityNotFoundException("Unit not found.");

        // The property a unit belongs to never changes through this path.
        var scoped = input with { PropertyId = unit.PropertyId };
        await EnsureUnitTypeSelectableAsync(scoped.UnitTypeId, currentUnitTypeId: unit.UnitTypeId, ct);
        await EnsureUnitNumberUniqueAsync(scoped, ct);

        Apply(scoped, unit);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteUnitAsync(int id, CancellationToken ct = default)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new EntityNotFoundException("Unit not found.");

        if (await db.Leases.AnyAsync(l => l.UnitId == id, ct))
            throw new DomainException("This unit cannot be removed because it has a lease.");
        if (await db.RentalApplications.AnyAsync(a => a.UnitId == id, ct))
            throw new DomainException("This unit cannot be removed because it has rental applications.");

        db.Units.Remove(unit);
        await db.SaveChangesAsync(ct);
    }

    public async Task<UnitBrowseResult> BrowseUnitsAsync(UnitBrowseFilter filter, string? applicantId, CancellationToken ct = default)
    {
        var today = Today;
        var openStatuses = ApplicationWorkflow.OpenStatuses;

        var query = db.Units.Where(u => !u.Leases.Any(l => l.StartDate <= today && l.EndDate >= today));

        if (filter.PropertyId.HasValue) query = query.Where(u => u.PropertyId == filter.PropertyId);
        if (filter.UnitTypeId.HasValue) query = query.Where(u => u.UnitTypeId == filter.UnitTypeId);
        if (filter.MinBedrooms.HasValue) query = query.Where(u => u.Bedrooms >= filter.MinBedrooms);
        if (filter.MaxRent.HasValue) query = query.Where(u => u.MonthlyRent <= filter.MaxRent);

        var units = await query
            .OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber)
            .Select(u => new UnitCard
            {
                UnitId = u.Id,
                PropertyName = u.Property.Name,
                Address = u.Property.StreetAddress + ", " + u.Property.City + ", " + u.Property.State,
                UnitNumber = u.UnitNumber,
                UnitType = u.UnitType.Name,
                Bedrooms = u.Bedrooms,
                MonthlyRent = u.MonthlyRent,
                OpenApplicationId = applicantId == null
                    ? null
                    : u.Applications
                        .Where(a => a.ApplicantId == applicantId && openStatuses.Contains(a.Status))
                        .Select(a => (int?)a.Id)
                        .FirstOrDefault()
            })
            .ToListAsync(ct);

        return new UnitBrowseResult
        {
            Filter = filter,
            Units = units,
            Properties = await db.Properties.OrderBy(p => p.Name).Select(p => new LookupItem { Id = p.Id, Name = p.Name }).ToListAsync(ct),
            UnitTypes = await db.UnitTypes.Where(t => t.IsActive).OrderBy(t => t.SortOrder).Select(t => new LookupItem { Id = t.Id, Name = t.Name }).ToListAsync(ct)
        };
    }

    private static void Apply(PropertyInput input, Property property)
    {
        property.Name = input.Name.Trim();
        property.StreetAddress = input.StreetAddress.Trim();
        property.City = input.City.Trim();
        property.State = input.State.Trim().ToUpperInvariant();
        property.PostalCode = input.PostalCode.Trim();
    }

    private static void Apply(UnitInput input, Unit unit)
    {
        unit.UnitNumber = input.UnitNumber.Trim();
        unit.Bedrooms = input.Bedrooms;
        unit.MonthlyRent = input.MonthlyRent;
        unit.UnitTypeId = input.UnitTypeId;
    }

    private async Task EnsureUnitTypeSelectableAsync(int unitTypeId, int? currentUnitTypeId, CancellationToken ct)
    {
        var type = await db.UnitTypes.FirstOrDefaultAsync(t => t.Id == unitTypeId, ct)
            ?? throw new DomainException("Please choose a unit type.", nameof(UnitInput.UnitTypeId));

        if (!type.IsSelectableFor(currentUnitTypeId))
            throw new DomainException($"The unit type '{type.Name}' is inactive and cannot be selected.", nameof(UnitInput.UnitTypeId));
    }

    private async Task EnsureUnitNumberUniqueAsync(UnitInput input, CancellationToken ct)
    {
        var number = input.UnitNumber.Trim();
        var duplicate = await db.Units.AnyAsync(u => u.PropertyId == input.PropertyId && u.UnitNumber == number && u.Id != input.Id, ct);
        if (duplicate)
            throw new DomainException($"Unit {number} already exists in this property.", nameof(UnitInput.UnitNumber));
    }
}
