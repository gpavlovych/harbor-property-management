using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Persistence;

namespace PropertyManagement.Application.Tests.Support;

/// <summary>
/// Builds an isolated in-memory database with a minimal, known data set. The real EF context from Infrastructure is used
/// so the tests exercise the same model configuration (including the user projection) as production.
/// </summary>
public static class TestDb
{
    public static readonly DateOnly Today = new(2026, 9, 10);
    public static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    public const string Applicant = "applicant";
    public const string OtherApplicant = "applicant2";
    public const string Manager = "manager";

    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);
        Seed(db);
        return db;
    }

    public static FixedTimeProvider Time() => new(Now);

    private static void Seed(ApplicationDbContext db)
    {
        db.Users.AddRange(
            new ApplicationUser { Id = Applicant, UserName = "a@example.com", Email = "a@example.com", FullName = "Alice Applicant", PhoneNumber = "555-0100" },
            new ApplicationUser { Id = OtherApplicant, UserName = "b@example.com", Email = "b@example.com", FullName = "Bob Applicant" },
            new ApplicationUser { Id = Manager, UserName = "m@example.com", Email = "m@example.com", FullName = "Mary Manager" });

        db.UnitTypes.AddRange(
            new UnitType { Id = 1, Name = "1 Bedroom", IsActive = true, SortOrder = 1 },
            new UnitType { Id = 2, Name = "Loft", IsActive = false, SortOrder = 2 });

        db.Properties.Add(new Property { Id = 1, Name = "Test Towers", StreetAddress = "1 Main St", City = "Town", State = "CT", PostalCode = "06000" });

        db.Units.AddRange(
            new Unit { Id = 1, PropertyId = 1, UnitTypeId = 1, UnitNumber = "1A", Bedrooms = 1, MonthlyRent = 1500 },   // available
            new Unit { Id = 2, PropertyId = 1, UnitTypeId = 1, UnitNumber = "1B", Bedrooms = 1, MonthlyRent = 1600 },   // leased today
            new Unit { Id = 3, PropertyId = 1, UnitTypeId = 2, UnitNumber = "1C", Bedrooms = 2, MonthlyRent = 2000 },   // uses inactive type
            new Unit { Id = 4, PropertyId = 1, UnitTypeId = 1, UnitNumber = "1D", Bedrooms = 1, MonthlyRent = 1700 });  // future lease only

        AddApproved(db, 100, unitId: 2, leaseStart: Today.AddMonths(-2), rent: 1600);
        AddApproved(db, 101, unitId: 4, leaseStart: Today.AddMonths(1), rent: 1700);

        db.SaveChanges();
    }

    private static void AddApproved(ApplicationDbContext db, int id, int unitId, DateOnly leaseStart, decimal rent)
    {
        db.RentalApplications.Add(new RentalApplication
        {
            Id = id, UnitId = unitId, ApplicantId = OtherApplicant, Status = ApplicationStatus.Approved,
            FullName = "Bob", Phone = "1", Email = "b@example.com", CurrentAddress = "x",
            ApplicantInformationCompletedAt = Now.UtcDateTime, ResidenceHistoryCompletedAt = Now.UtcDateTime,
            CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime
        });
        db.Leases.Add(new Lease
        {
            UnitId = unitId, RentalApplicationId = id, TenantId = OtherApplicant,
            StartDate = leaseStart, EndDate = LeaseRules.EndDateFor(leaseStart), MonthlyRent = rent
        });
    }
}
