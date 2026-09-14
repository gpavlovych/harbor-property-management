using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Catalog;
using PropertyManagement.Application.Catalog.Models;
using PropertyManagement.Application.Tests.Support;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Infrastructure.Persistence;

namespace PropertyManagement.Application.Tests;

public class CatalogServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly CatalogService _service;

    public CatalogServiceTests() => _service = new CatalogService(_db, TestDb.Time());

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Inactive_unit_types_are_not_offered_for_new_units_but_stay_on_units_that_use_them()
    {
        var forNew = await _service.GetUnitTypeOptionsAsync(null);
        Assert.Single(forNew);
        Assert.Equal("1 Bedroom", forNew[0].Name);

        var forExisting = await _service.GetUnitTypeOptionsAsync(2);
        Assert.Equal(2, forExisting.Count);
        Assert.Contains(forExisting, o => o.Id == 2 && o.Name.Contains("inactive"));
    }

    [Fact]
    public async Task Creating_a_unit_with_an_inactive_type_is_rejected_on_the_server()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.CreateUnitAsync(new UnitInput(0, 1, "9Z", 2, 1000, 2)));
        Assert.Equal(nameof(UnitInput.UnitTypeId), ex.MemberName);
    }

    [Fact]
    public async Task A_unit_that_already_uses_an_inactive_type_can_be_edited_and_keep_it()
    {
        await _service.UpdateUnitAsync(new UnitInput(3, 1, "1C", 3, 2100, 2));

        var unit = await _db.Units.SingleAsync(u => u.Id == 3);
        Assert.Equal(2, unit.UnitTypeId);
        Assert.Equal(2100m, unit.MonthlyRent);
    }

    [Fact]
    public async Task Switching_another_unit_to_an_inactive_type_is_rejected() =>
        await Assert.ThrowsAsync<DomainException>(() => _service.UpdateUnitAsync(new UnitInput(1, 1, "1A", 1, 1500, 2)));

    [Fact]
    public async Task Unit_numbers_are_unique_within_a_property()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.CreateUnitAsync(new UnitInput(0, 1, "1A", 1, 1000, 1)));
        Assert.Equal(nameof(UnitInput.UnitNumber), ex.MemberName);
    }

    [Fact]
    public async Task Property_input_is_normalised()
    {
        var id = await _service.CreatePropertyAsync(new PropertyInput(0, "  New Place ", "1 Rd", "Town", "ct", "12345"));
        var property = await _db.Properties.SingleAsync(p => p.Id == id);
        Assert.Equal("New Place", property.Name);
        Assert.Equal("CT", property.State);
    }

    [Fact]
    public async Task Browse_only_lists_units_without_a_lease_covering_today()
    {
        var result = await _service.BrowseUnitsAsync(new UnitBrowseFilter(), applicantId: null);
        var ids = result.Units.Select(u => u.UnitId).ToList();

        Assert.Contains(1, ids);
        Assert.Contains(3, ids);
        Assert.Contains(4, ids);      // lease starts next month, so it is available today
        Assert.DoesNotContain(2, ids); // leased today
    }

    [Fact]
    public async Task Browse_filters_are_applied()
    {
        var result = await _service.BrowseUnitsAsync(new UnitBrowseFilter { MinBedrooms = 2, MaxRent = 2000 }, applicantId: null);
        Assert.Equal([3], result.Units.Select(u => u.UnitId));
    }

    [Fact]
    public async Task Units_and_properties_with_leases_or_applications_cannot_be_removed()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.DeleteUnitAsync(2));
        await Assert.ThrowsAsync<DomainException>(() => _service.DeletePropertyAsync(1));

        await _service.DeleteUnitAsync(1);
        Assert.Null(await _db.Units.FindAsync(1));
    }
}
