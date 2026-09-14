using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Domain.Tests;

public class LeaseRulesTests
{
    private static Lease LeaseFrom(DateOnly start) => new() { StartDate = start, EndDate = LeaseRules.EndDateFor(start) };

    [Fact]
    public void Term_is_twelve_months_ending_the_day_before_the_anniversary()
    {
        Assert.Equal(new DateOnly(2027, 2, 28), LeaseRules.EndDateFor(new DateOnly(2026, 3, 1)));
        Assert.Equal(new DateOnly(2026, 12, 31), LeaseRules.EndDateFor(new DateOnly(2026, 1, 1)));
    }

    [Theory]
    [InlineData("2026-01-01", "2026-01-01", true)]
    [InlineData("2026-01-01", "2026-06-15", true)]
    [InlineData("2026-01-01", "2026-12-31", true)]
    [InlineData("2026-01-01", "2027-01-01", false)]
    [InlineData("2026-01-01", "2025-12-31", false)]
    public void A_lease_is_active_only_while_its_term_covers_the_date(string start, string date, bool expected) =>
        Assert.Equal(expected, LeaseRules.IsActiveOn(LeaseFrom(DateOnly.Parse(start)), DateOnly.Parse(date)));

    [Fact]
    public void A_unit_is_available_when_no_lease_covers_today()
    {
        var today = new DateOnly(2026, 9, 10);
        Assert.True(LeaseRules.IsUnitAvailableOn([], today));
        Assert.True(LeaseRules.IsUnitAvailableOn([LeaseFrom(today.AddYears(-2))], today));
        Assert.True(LeaseRules.IsUnitAvailableOn([LeaseFrom(today.AddMonths(1))], today));
        Assert.False(LeaseRules.IsUnitAvailableOn([LeaseFrom(today.AddMonths(-1))], today));

        var unit = new Unit { Leases = [LeaseFrom(today.AddMonths(-1))] };
        Assert.False(unit.IsAvailableOn(today));
    }

    [Fact]
    public void A_second_lease_conflicts_when_one_is_active_today_or_overlaps_the_new_term()
    {
        var today = new DateOnly(2026, 9, 10);
        Assert.False(LeaseRules.ConflictsWithExisting([], today, today));
        Assert.False(LeaseRules.ConflictsWithExisting([LeaseFrom(today.AddYears(-2))], today, today));
        Assert.True(LeaseRules.ConflictsWithExisting([LeaseFrom(today.AddMonths(-1))], today, today.AddYears(2)));
        Assert.True(LeaseRules.ConflictsWithExisting([LeaseFrom(today.AddMonths(6))], today, today.AddMonths(1)));
        Assert.False(LeaseRules.ConflictsWithExisting([LeaseFrom(today.AddMonths(13))], today, today));
    }
}
