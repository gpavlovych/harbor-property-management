using PropertyManagement.Application.Applications;
using PropertyManagement.Application.Applications.Models;
using PropertyManagement.Application.Tests.Support;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;

namespace PropertyManagement.Application.Tests;

public class ApplicationQueryServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly ApplicationQueryService _queries;
    private readonly RentalApplicationService _commands;

    public ApplicationQueryServiceTests()
    {
        _queries = new ApplicationQueryService(_db, TestDb.Time());
        _commands = new RentalApplicationService(_db, TestDb.Time());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Applicants_only_see_their_own_applications_and_managers_see_all()
    {
        await _commands.StartAsync(1, TestDb.Applicant);

        var mine = await _queries.ListAsync(new ApplicationListFilter(), TestDb.Applicant);
        var all = await _queries.ListAsync(new ApplicationListFilter(), null);

        Assert.Single(mine.Results.Items);
        Assert.Equal("Alice Applicant", mine.Results.Items[0].ApplicantName);
        Assert.Equal(3, all.Results.TotalCount);
    }

    [Fact]
    public async Task List_filters_by_status_and_sorts_by_applicant_name()
    {
        await _commands.StartAsync(1, TestDb.Applicant);

        var approved = await _queries.ListAsync(new ApplicationListFilter { Status = ApplicationStatus.Approved }, null);
        Assert.Equal(2, approved.Results.TotalCount);
        Assert.All(approved.Results.Items, i => Assert.Equal(ApplicationStatus.Approved, i.Status));

        var byName = await _queries.ListAsync(new ApplicationListFilter { Sort = ApplicationSort.Applicant, Desc = false }, null);
        Assert.Equal(["Alice Applicant", "Bob Applicant", "Bob Applicant"], byName.Results.Items.Select(i => i.ApplicantName));
    }

    [Fact]
    public async Task Details_decide_editability_and_hide_manager_notes_from_applicants()
    {
        var id = await _commands.StartAsync(1, TestDb.Applicant);
        await _commands.SaveManagerNotesAsync(id, "secret");

        var asOwner = await _queries.GetDetailsAsync(id, TestDb.Applicant, isManager: false, null);
        var asManager = await _queries.GetDetailsAsync(id, TestDb.Manager, isManager: true, null);
        var asStranger = await _queries.GetDetailsAsync(id, TestDb.OtherApplicant, isManager: false, null);

        Assert.NotNull(asOwner);
        Assert.True(asOwner.IsEditable);
        Assert.Null(asOwner.ManagerNotes);
        Assert.Equal(ApplicationSection.ApplicantInformation, asOwner.CurrentSection);

        Assert.NotNull(asManager);
        Assert.False(asManager.IsEditable);
        Assert.Equal("secret", asManager.ManagerNotes);
        Assert.Equal(ApplicationSection.Summary, asManager.CurrentSection);
        Assert.Equal("Alice Applicant", asManager.ApplicantName);

        Assert.Null(asStranger);
    }

    [Fact]
    public async Task History_resolves_the_name_of_the_person_who_made_each_change()
    {
        var id = await _commands.StartAsync(1, TestDb.Applicant);
        var history = await _queries.GetHistoryAsync(id);

        var entry = Assert.Single(history);
        Assert.Equal("Alice Applicant", entry.ChangedBy);
        Assert.Equal(ApplicationStatus.Draft, entry.ToStatus);
    }

    [Fact]
    public async Task Leases_are_listed_with_tenant_names_and_activity()
    {
        var all = await _queries.ListLeasesAsync(null);
        var bobs = await _queries.ListLeasesAsync(TestDb.OtherApplicant);
        var alices = await _queries.ListLeasesAsync(TestDb.Applicant);

        Assert.Equal(2, all.Count);
        Assert.Equal(2, bobs.Count);
        Assert.Empty(alices);
        Assert.Contains(all, l => l.IsActive);
        Assert.Contains(all, l => !l.IsActive);
        Assert.All(all, l => Assert.Equal("Bob Applicant", l.TenantName));
    }
}
