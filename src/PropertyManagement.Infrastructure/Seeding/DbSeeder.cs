using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PropertyManagement.Application.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Persistence;

namespace PropertyManagement.Infrastructure.Seeding;

/// <summary>
/// Applies migrations and seeds the database idempotently. Every seed step checks for existing data first,
/// and Bogus uses a fixed seed so the generated data is the same on every machine.
/// </summary>
public static class DbSeeder
{
    public const string DefaultPassword = "Password1!";

    private static readonly string[] ActiveUnitTypes = ["Studio", "1 Bedroom", "2 Bedroom", "3 Bedroom", "Townhouse"];
    private static readonly string[] InactiveUnitTypes = ["Penthouse", "Loft"];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await db.Database.MigrateAsync(ct);

        Randomizer.Seed = new Random(20260910);

        await SeedRolesAsync(roleManager);
        var unitTypes = await SeedUnitTypesAsync(db, ct);
        var managers = await SeedUsersAsync(userManager, Roles.PropertyManager, "manager", 2);
        var applicants = await SeedUsersAsync(userManager, Roles.Applicant, "applicant", 6);
        var units = await SeedPropertiesAndUnitsAsync(db, unitTypes, ct);
        await SeedApplicationsAsync(db, units, applicants, managers, ct);

        logger.LogInformation("Database seeded. Managers: {Managers}, applicants: {Applicants}", managers.Count, applicants.Count);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task<List<UnitType>> SeedUnitTypesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var existing = await db.UnitTypes.ToListAsync(ct);
        var order = 0;
        foreach (var name in ActiveUnitTypes.Concat(InactiveUnitTypes))
        {
            order++;
            if (existing.Any(t => t.Name == name)) continue;
            var type = new UnitType { Name = name, IsActive = !InactiveUnitTypes.Contains(name), SortOrder = order };
            db.UnitTypes.Add(type);
            existing.Add(type);
        }
        await db.SaveChangesAsync(ct);
        return existing.OrderBy(t => t.SortOrder).ToList();
    }

    private static async Task<List<ApplicationUser>> SeedUsersAsync(UserManager<ApplicationUser> userManager, string role, string prefix, int count)
    {
        var faker = new Faker();
        var users = new List<ApplicationUser>();
        for (var i = 1; i <= count; i++)
        {
            var email = $"{prefix}{i}@example.com";
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = faker.Name.FullName(),
                    PhoneNumber = faker.Phone.PhoneNumber("###-###-####")
                };
                var result = await userManager.CreateAsync(user, DefaultPassword);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
            users.Add(user);
        }
        return users;
    }

    private static async Task<List<Unit>> SeedPropertiesAndUnitsAsync(ApplicationDbContext db, List<UnitType> unitTypes, CancellationToken ct)
    {
        if (await db.Properties.AnyAsync(ct))
        {
            return await db.Units.Include(u => u.Property).Include(u => u.UnitType).Include(u => u.Leases).ToListAsync(ct);
        }

        var activeTypes = unitTypes.Where(t => t.IsActive).ToList();
        var inactiveType = unitTypes.First(t => !t.IsActive);

        var propertyFaker = new Faker<Property>()
            .RuleFor(p => p.Name, f => $"{f.Address.StreetName()} {f.PickRandom("Apartments", "Residences", "Lofts", "Commons", "Place")}")
            .RuleFor(p => p.StreetAddress, f => f.Address.StreetAddress())
            .RuleFor(p => p.City, f => f.Address.City())
            .RuleFor(p => p.State, f => f.Address.StateAbbr())
            .RuleFor(p => p.PostalCode, f => f.Address.ZipCode("#####"))
            .RuleFor(p => p.CreatedAt, f => f.Date.Past(2).ToUniversalTime());

        var properties = propertyFaker.Generate(5);
        var faker = new Faker();
        var units = new List<Unit>();

        foreach (var property in properties)
        {
            var unitCount = faker.Random.Int(4, 8);
            for (var i = 0; i < unitCount; i++)
            {
                var type = faker.PickRandom(activeTypes);
                units.Add(new Unit
                {
                    Property = property,
                    UnitNumber = $"{i / 4 + 1}{(char)('A' + i % 4)}",
                    UnitType = type,
                    Bedrooms = BedroomsFor(type, faker),
                    MonthlyRent = Math.Round(faker.Random.Decimal(900, 3400) / 25) * 25,
                    CreatedAt = property.CreatedAt
                });
            }
        }

        // One unit uses an inactive lookup value so the "still displays but cannot be selected" rule is visible.
        var legacy = units.Last();
        legacy.UnitType = inactiveType;
        legacy.Bedrooms = 3;

        db.Properties.AddRange(properties);
        db.Units.AddRange(units);
        await db.SaveChangesAsync(ct);
        return units;
    }

    private static int BedroomsFor(UnitType type, Faker faker) => type.Name switch
    {
        "Studio" => 0,
        "1 Bedroom" => 1,
        "2 Bedroom" => 2,
        "3 Bedroom" => 3,
        _ => faker.Random.Int(2, 4)
    };

    private static async Task SeedApplicationsAsync(ApplicationDbContext db, List<Unit> units, List<ApplicationUser> applicants,
        List<ApplicationUser> managers, CancellationToken ct)
    {
        if (await db.RentalApplications.AnyAsync(ct)) return;

        var faker = new Faker();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var shuffledUnits = new Queue<Unit>(faker.Random.Shuffle(units));
        var residenceFaker = new Faker<Residence>()
            .RuleFor(r => r.Address, f => f.Address.FullAddress())
            .RuleFor(r => r.LandlordName, f => f.Name.FullName())
            .RuleFor(r => r.LandlordPhone, f => f.Phone.PhoneNumber("###-###-####"));

        // Status plan: every status is represented, Approved three times (two active leases, one expired lease).
        var plan = new List<(ApplicationStatus Status, bool ActiveLease)>
        {
            (ApplicationStatus.Draft, false),
            (ApplicationStatus.Draft, false),
            (ApplicationStatus.Submitted, false),
            (ApplicationStatus.Submitted, false),
            (ApplicationStatus.Returned, false),
            (ApplicationStatus.Approved, true),
            (ApplicationStatus.Approved, true),
            (ApplicationStatus.Approved, false),
            (ApplicationStatus.Denied, false),
            (ApplicationStatus.Withdrawn, false)
        };

        var leasedUnits = new List<Unit>();
        var applicantIndex = 0;

        foreach (var (status, activeLease) in plan)
        {
            var unit = shuffledUnits.Dequeue();
            var applicant = applicants[applicantIndex++ % applicants.Count];
            var manager = faker.PickRandom(managers);
            var created = faker.Date.Past(1, DateTime.UtcNow.AddDays(-14)).ToUniversalTime();

            var application = RentalApplication.Start(unit.Id, applicant.Id, applicant.FullName, applicant.Email, applicant.PhoneNumber, created);
            application.Unit = unit;
            application.CurrentAddress = faker.Address.FullAddress();
            application.ApplicantInformationCompletedAt = created;

            var isDraft = status == ApplicationStatus.Draft;
            if (!isDraft || faker.Random.Bool())
            {
                var residences = residenceFaker.Generate(faker.Random.Int(1, 3));
                var cursor = created.AddYears(-1);
                foreach (var residence in residences.AsEnumerable().Reverse())
                {
                    var moveOut = DateOnly.FromDateTime(cursor);
                    var moveIn = moveOut.AddMonths(-faker.Random.Int(6, 30));
                    residence.MoveInDate = moveIn;
                    residence.MoveOutDate = moveOut;
                    cursor = moveIn.ToDateTime(TimeOnly.MinValue).AddDays(-1);
                    application.Residences.Add(residence);
                }
                if (!isDraft) application.ResidenceHistoryCompletedAt = created.AddHours(1);
            }

            if (!isDraft)
            {
                var submitted = created.AddDays(1);
                application.TransitionTo(ApplicationStatus.Submitted, applicant.Id, submitted, null);
                application.SubmittedAt = submitted;
            }

            switch (status)
            {
                case ApplicationStatus.Returned:
                    Review(application, manager.Id, ApplicationStatus.Returned, "Please add your most recent landlord's phone number.");
                    break;
                case ApplicationStatus.Denied:
                    Review(application, manager.Id, ApplicationStatus.Denied, "Income documentation did not meet the minimum requirement.");
                    break;
                case ApplicationStatus.Withdrawn:
                    application.TransitionTo(ApplicationStatus.Withdrawn, applicant.Id, application.SubmittedAt!.Value.AddDays(2), "Found another place.");
                    break;
                case ApplicationStatus.Approved:
                    Review(application, manager.Id, ApplicationStatus.Approved, "Approved. Welcome aboard!");
                    var start = activeLease ? today.AddMonths(-faker.Random.Int(1, 10)) : today.AddMonths(-14);
                    application.IssueLease(start, today, application.ReviewedAt!.Value);
                    if (activeLease) leasedUnits.Add(unit);
                    break;
            }

            db.RentalApplications.Add(application);
        }

        // A submitted application for a unit that already has an active lease demonstrates the approval guard.
        var blockedApplicant = applicants.Last();
        var blockedUnit = leasedUnits.First();
        var blockedCreated = DateTime.UtcNow.AddDays(-3);
        var blocked = RentalApplication.Start(blockedUnit.Id, blockedApplicant.Id, blockedApplicant.FullName, blockedApplicant.Email, blockedApplicant.PhoneNumber, blockedCreated);
        blocked.CurrentAddress = faker.Address.FullAddress();
        blocked.ApplicantInformationCompletedAt = blockedCreated;
        blocked.ResidenceHistoryCompletedAt = blockedCreated.AddHours(1);
        var prior = residenceFaker.Generate();
        prior.MoveOutDate = today.AddMonths(-1);
        prior.MoveInDate = prior.MoveOutDate.AddYears(-2);
        blocked.Residences.Add(prior);
        blocked.TransitionTo(ApplicationStatus.Submitted, blockedApplicant.Id, blockedCreated.AddHours(2), null);
        blocked.SubmittedAt = blockedCreated.AddHours(2);
        db.RentalApplications.Add(blocked);

        await db.SaveChangesAsync(ct);
    }

    private static void Review(RentalApplication application, string managerId, ApplicationStatus outcome, string comment)
    {
        var reviewed = application.SubmittedAt!.Value.AddDays(2);
        application.TransitionTo(outcome, managerId, reviewed, comment);
        application.ReviewedAt = reviewed;
        application.ReviewComment = comment;
    }
}
