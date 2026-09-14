using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Domain.Tests;

public class RentalApplicationTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 9, 10);

    [Fact]
    public void Start_creates_a_prefilled_draft_with_an_opening_history_entry()
    {
        var app = RentalApplication.Start(7, "u1", "Alice", "a@example.com", "555", Now);

        Assert.Equal(ApplicationStatus.Draft, app.Status);
        Assert.Equal(7, app.UnitId);
        Assert.Equal("Alice", app.FullName);
        Assert.True(app.IsEditable);
        Assert.False(app.AllSectionsCompleted);
        var entry = Assert.Single(app.History);
        Assert.Null(entry.FromStatus);
        Assert.Equal(ApplicationStatus.Draft, entry.ToStatus);
        Assert.Equal("u1", entry.ChangedByUserId);
    }

    [Fact]
    public void TransitionTo_records_history_and_rejects_illegal_moves()
    {
        var app = RentalApplication.Start(1, "u1", null, null, null, Now);

        app.TransitionTo(ApplicationStatus.Submitted, "u1", Now.AddHours(1), "  ");
        Assert.Equal(ApplicationStatus.Submitted, app.Status);
        Assert.Equal(Now.AddHours(1), app.UpdatedAt);
        var last = app.History.Last();
        Assert.Equal(ApplicationStatus.Draft, last.FromStatus);
        Assert.Equal(ApplicationStatus.Submitted, last.ToStatus);
        Assert.Null(last.Comment);

        var ex = Assert.Throws<DomainException>(() => app.TransitionTo(ApplicationStatus.Draft, "u1", Now, null));
        Assert.Contains("Cannot move", ex.Message);
        Assert.Equal(2, app.History.Count);
    }

    [Fact]
    public void Ownership_and_editability_guards()
    {
        var app = RentalApplication.Start(1, "u1", null, null, null, Now);
        app.EnsureOwnedBy("u1");
        Assert.Throws<UnauthorizedAccessException>(() => app.EnsureOwnedBy("u2"));

        app.EnsureEditable();
        app.TransitionTo(ApplicationStatus.Submitted, "u1", Now, null);
        Assert.Throws<DomainException>(app.EnsureEditable);
    }

    [Fact]
    public void IssueLease_creates_a_twelve_month_lease_for_the_applicant()
    {
        var unit = new Unit { Id = 1, MonthlyRent = 1500 };
        var app = RentalApplication.Start(1, "u1", null, null, null, Now);
        app.Unit = unit;

        var lease = app.IssueLease(Today.AddDays(5), Today, Now);

        Assert.Same(lease, app.Lease);
        Assert.Equal("u1", lease.TenantId);
        Assert.Equal(1500m, lease.MonthlyRent);
        Assert.Equal(Today.AddDays(5), lease.StartDate);
        Assert.Equal(LeaseRules.EndDateFor(Today.AddDays(5)), lease.EndDate);
    }

    [Fact]
    public void IssueLease_is_rejected_when_the_unit_already_has_a_conflicting_lease()
    {
        var unit = new Unit { Id = 1, MonthlyRent = 1500 };
        unit.Leases.Add(new Lease { StartDate = Today.AddMonths(-1), EndDate = LeaseRules.EndDateFor(Today.AddMonths(-1)) });
        var app = RentalApplication.Start(1, "u1", null, null, null, Now);
        app.Unit = unit;

        Assert.Throws<DomainException>(() => app.IssueLease(Today, Today, Now));
        Assert.Null(app.Lease);
    }

    [Fact]
    public void Inactive_unit_types_are_selectable_only_by_the_unit_that_uses_them()
    {
        var inactive = new UnitType { Id = 2, IsActive = false };
        Assert.True(inactive.IsSelectableFor(2));
        Assert.False(inactive.IsSelectableFor(1));
        Assert.False(inactive.IsSelectableFor(null));
        Assert.True(new UnitType { Id = 1, IsActive = true }.IsSelectableFor(null));
    }
}
