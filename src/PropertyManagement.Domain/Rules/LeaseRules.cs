using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Domain.Rules;

/// <summary>Pure lease rules shared by queries, use cases and unit tests.</summary>
public static class LeaseRules
{
    public const int TermMonths = 12;

    public static DateOnly EndDateFor(DateOnly start) => start.AddMonths(TermMonths).AddDays(-1);

    /// <summary>A lease is active on a date when its term covers that date.</summary>
    public static bool IsActiveOn(Lease lease, DateOnly date) => lease.StartDate <= date && lease.EndDate >= date;

    public static bool Overlaps(Lease lease, DateOnly start, DateOnly end) => lease.StartDate <= end && lease.EndDate >= start;

    /// <summary>A unit is available when no lease term covers the given date.</summary>
    public static bool IsUnitAvailableOn(IEnumerable<Lease> leases, DateOnly date) => !leases.Any(l => IsActiveOn(l, date));

    /// <summary>A second lease is rejected when any existing lease is active today or overlaps the proposed term.</summary>
    public static bool ConflictsWithExisting(IEnumerable<Lease> leases, DateOnly today, DateOnly proposedStart) =>
        leases.Any(l => IsActiveOn(l, today) || Overlaps(l, proposedStart, EndDateFor(proposedStart)));
}
