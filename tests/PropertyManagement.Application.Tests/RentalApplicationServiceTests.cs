using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Applications;
using PropertyManagement.Application.Applications.Models;
using PropertyManagement.Application.Tests.Support;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Persistence;

namespace PropertyManagement.Application.Tests;

public class RentalApplicationServiceTests : IDisposable
{
    private static readonly ApplicantInformationInput ValidInfo = new("Alice Applicant", "555-0100", "a@example.com", "2 Side St");

    private static ResidenceInput ValidResidence(int applicationId) =>
        new(0, applicationId, "9 Old Rd", "Larry", "555-0199", TestDb.Today.AddYears(-3), TestDb.Today.AddYears(-1));

    private readonly ApplicationDbContext _db = TestDb.Create();
    private readonly RentalApplicationService _service;

    public RentalApplicationServiceTests() => _service = new RentalApplicationService(_db, TestDb.Time());

    public void Dispose() => _db.Dispose();

    private async Task<int> CompleteDraftAsync(int unitId = 1)
    {
        var id = await _service.StartAsync(unitId, TestDb.Applicant);
        await _service.SaveApplicantInformationAsync(id, TestDb.Applicant, ValidInfo);
        await _service.AddResidenceAsync(TestDb.Applicant, ValidResidence(id));
        await _service.CompleteResidenceHistoryAsync(id, TestDb.Applicant);
        return id;
    }

    private Task<RentalApplication> LoadAsync(int id) =>
        _db.RentalApplications.Include(a => a.History).Include(a => a.Lease).SingleAsync(a => a.Id == id);

    [Fact]
    public async Task Start_creates_a_draft_prefilled_from_the_user_profile()
    {
        var id = await _service.StartAsync(1, TestDb.Applicant);
        var app = await LoadAsync(id);

        Assert.Equal(ApplicationStatus.Draft, app.Status);
        Assert.Equal("Alice Applicant", app.FullName);
        Assert.Equal("a@example.com", app.Email);
        Assert.Equal("555-0100", app.Phone);
        Assert.Single(app.History);
    }

    [Fact]
    public async Task Start_returns_the_existing_open_application_for_the_same_unit()
    {
        var first = await _service.StartAsync(1, TestDb.Applicant);
        var second = await _service.StartAsync(1, TestDb.Applicant);

        Assert.Equal(first, second);
        Assert.Equal(1, await _db.RentalApplications.CountAsync(a => a.ApplicantId == TestDb.Applicant));
    }

    [Fact]
    public async Task Start_is_rejected_for_a_unit_with_an_active_lease()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.StartAsync(2, TestDb.Applicant));
        Assert.Contains("not available", ex.Message);
    }

    [Fact]
    public async Task Saving_applicant_information_marks_the_section_complete()
    {
        var id = await _service.StartAsync(1, TestDb.Applicant);
        await _service.SaveApplicantInformationAsync(id, TestDb.Applicant, ValidInfo);

        var saved = await LoadAsync(id);
        Assert.NotNull(saved.ApplicantInformationCompletedAt);
        Assert.Equal("2 Side St", saved.CurrentAddress);
    }

    [Fact]
    public async Task Another_applicant_cannot_edit_the_application()
    {
        var id = await _service.StartAsync(1, TestDb.Applicant);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.SaveApplicantInformationAsync(id, TestDb.OtherApplicant, ValidInfo));
    }

    [Fact]
    public async Task Residence_history_cannot_be_completed_without_a_residence()
    {
        var id = await _service.StartAsync(1, TestDb.Applicant);
        await Assert.ThrowsAsync<DomainException>(() => _service.CompleteResidenceHistoryAsync(id, TestDb.Applicant));
    }

    [Fact]
    public async Task Removing_the_last_residence_reopens_the_section()
    {
        var id = await _service.StartAsync(1, TestDb.Applicant);
        var residenceId = await _service.AddResidenceAsync(TestDb.Applicant, ValidResidence(id));
        await _service.CompleteResidenceHistoryAsync(id, TestDb.Applicant);
        await _service.RemoveResidenceAsync(id, residenceId, TestDb.Applicant);

        Assert.Null((await LoadAsync(id)).ResidenceHistoryCompletedAt);
    }

    [Fact]
    public async Task Residence_dates_are_validated_by_the_use_case()
    {
        var id = await _service.StartAsync(1, TestDb.Applicant);
        var future = ValidResidence(id) with { MoveOutDate = TestDb.Today.AddDays(1) };
        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.AddResidenceAsync(TestDb.Applicant, future));
        Assert.Equal(nameof(ResidenceInput.MoveOutDate), ex.MemberName);
    }

    [Fact]
    public async Task Submit_requires_both_sections_to_be_complete()
    {
        var id = await _service.StartAsync(1, TestDb.Applicant);
        await _service.SaveApplicantInformationAsync(id, TestDb.Applicant, ValidInfo);
        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.SubmitAsync(id, TestDb.Applicant));
        Assert.Contains("Complete every section", ex.Message);
    }

    [Fact]
    public async Task Submit_moves_to_Submitted_and_records_history()
    {
        var id = await CompleteDraftAsync();
        await _service.SubmitAsync(id, TestDb.Applicant);

        var saved = await LoadAsync(id);
        Assert.Equal(ApplicationStatus.Submitted, saved.Status);
        Assert.Equal(TestDb.Now.UtcDateTime, saved.SubmittedAt);
        Assert.Contains(saved.History, x => x.FromStatus == ApplicationStatus.Draft && x.ToStatus == ApplicationStatus.Submitted && x.ChangedByUserId == TestDb.Applicant);
    }

    [Fact]
    public async Task Submit_is_rejected_when_the_unit_gained_an_active_lease()
    {
        var id = await CompleteDraftAsync();

        // Someone else is approved for unit 1 in the meantime.
        var rival = new RentalApplication { Id = 300, UnitId = 1, ApplicantId = TestDb.OtherApplicant, Status = ApplicationStatus.Approved };
        _db.RentalApplications.Add(rival);
        _db.Leases.Add(new Lease { UnitId = 1, RentalApplication = rival, TenantId = TestDb.OtherApplicant, StartDate = TestDb.Today.AddDays(-1), EndDate = LeaseRules.EndDateFor(TestDb.Today.AddDays(-1)), MonthlyRent = 1 });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.SubmitAsync(id, TestDb.Applicant));
        Assert.Contains("active lease", ex.Message);
        Assert.Equal(ApplicationStatus.Draft, (await LoadAsync(id)).Status);
    }

    [Fact]
    public async Task Submitted_applications_cannot_be_edited()
    {
        var id = await CompleteDraftAsync();
        await _service.SubmitAsync(id, TestDb.Applicant);

        await Assert.ThrowsAsync<DomainException>(() => _service.SaveApplicantInformationAsync(id, TestDb.Applicant, ValidInfo));
        await Assert.ThrowsAsync<DomainException>(() => _service.AddResidenceAsync(TestDb.Applicant, ValidResidence(id)));
    }

    [Fact]
    public async Task Withdraw_is_allowed_from_open_statuses_only()
    {
        var id = await CompleteDraftAsync();
        await _service.SubmitAsync(id, TestDb.Applicant);
        await _service.WithdrawAsync(id, TestDb.Applicant, "Found another place");

        var saved = await LoadAsync(id);
        Assert.Equal(ApplicationStatus.Withdrawn, saved.Status);
        Assert.Equal("Found another place", saved.History.Last().Comment);

        await Assert.ThrowsAsync<DomainException>(() => _service.WithdrawAsync(id, TestDb.Applicant, null));
        await Assert.ThrowsAsync<DomainException>(() => _service.WithdrawAsync(100, TestDb.OtherApplicant, null)); // approved
    }

    [Fact]
    public async Task Return_requires_a_comment_and_lets_the_applicant_edit_and_resubmit()
    {
        var id = await CompleteDraftAsync();
        await _service.SubmitAsync(id, TestDb.Applicant);

        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.ReviewAsync(new ReviewInput(id, ReviewOutcome.Return, " ", null), TestDb.Manager));
        Assert.Equal(nameof(ReviewInput.Comment), ex.MemberName);

        await _service.ReviewAsync(new ReviewInput(id, ReviewOutcome.Return, "Fix your phone number", null), TestDb.Manager);
        var returned = await LoadAsync(id);
        Assert.Equal(ApplicationStatus.Returned, returned.Status);
        Assert.Equal("Fix your phone number", returned.ReviewComment);
        Assert.True(returned.IsEditable);

        await _service.SaveApplicantInformationAsync(id, TestDb.Applicant, ValidInfo);
        await _service.SubmitAsync(id, TestDb.Applicant);
        var resubmitted = await LoadAsync(id);
        Assert.Equal(ApplicationStatus.Submitted, resubmitted.Status);
        Assert.Null(resubmitted.ReviewComment);
        Assert.Equal(4, resubmitted.History.Count); // Draft, Submitted, Returned, Submitted
    }

    [Fact]
    public async Task Approve_issues_a_twelve_month_lease_and_records_the_reviewer()
    {
        var id = await CompleteDraftAsync();
        await _service.SubmitAsync(id, TestDb.Applicant);

        var start = TestDb.Today.AddDays(5);
        await _service.ReviewAsync(new ReviewInput(id, ReviewOutcome.Approve, "Welcome", start), TestDb.Manager);

        var saved = await LoadAsync(id);
        Assert.Equal(ApplicationStatus.Approved, saved.Status);
        Assert.NotNull(saved.Lease);
        Assert.Equal(start, saved.Lease.StartDate);
        Assert.Equal(start.AddMonths(12).AddDays(-1), saved.Lease.EndDate);
        Assert.Equal(1500m, saved.Lease.MonthlyRent);
        Assert.Equal(TestDb.Applicant, saved.Lease.TenantId);
        Assert.Contains(saved.History, x => x.ToStatus == ApplicationStatus.Approved && x.ChangedByUserId == TestDb.Manager && x.Comment == "Welcome");
        Assert.True(saved.IsTerminal);
    }

    [Fact]
    public async Task Approve_is_rejected_when_the_unit_already_has_an_active_lease()
    {
        // A submitted application for unit 2, which is leased today.
        _db.RentalApplications.Add(new RentalApplication
        {
            Id = 200, UnitId = 2, ApplicantId = TestDb.Applicant, Status = ApplicationStatus.Submitted,
            FullName = "A", Phone = "1", Email = "a@example.com", CurrentAddress = "x",
            ApplicantInformationCompletedAt = TestDb.Now.UtcDateTime, ResidenceHistoryCompletedAt = TestDb.Now.UtcDateTime
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.ReviewAsync(new ReviewInput(200, ReviewOutcome.Approve, null, TestDb.Today), TestDb.Manager));
        Assert.Contains("active lease", ex.Message);
        Assert.Equal(nameof(ReviewInput.LeaseStartDate), ex.MemberName);
        Assert.Equal(ApplicationStatus.Submitted, (await LoadAsync(200)).Status);
        Assert.Equal(1, await _db.Leases.CountAsync(l => l.UnitId == 2));
    }

    [Fact]
    public async Task Approve_is_rejected_when_the_new_term_would_overlap_a_future_lease()
    {
        // Unit 4 has a lease starting next month; it is available today but a second overlapping lease is not allowed.
        var id = await CompleteDraftAsync(unitId: 4);
        await _service.SubmitAsync(id, TestDb.Applicant);

        await Assert.ThrowsAsync<DomainException>(() => _service.ReviewAsync(new ReviewInput(id, ReviewOutcome.Approve, null, TestDb.Today), TestDb.Manager));
    }

    [Fact]
    public async Task Deny_is_terminal_and_other_applications_are_left_alone()
    {
        var id = await CompleteDraftAsync();
        await _service.SubmitAsync(id, TestDb.Applicant);
        var other = await _service.StartAsync(1, TestDb.OtherApplicant);

        await _service.ReviewAsync(new ReviewInput(id, ReviewOutcome.Deny, "Insufficient income", null), TestDb.Manager);

        Assert.Equal(ApplicationStatus.Denied, (await LoadAsync(id)).Status);
        Assert.Equal(ApplicationStatus.Draft, (await LoadAsync(other)).Status);
        await Assert.ThrowsAsync<DomainException>(() => _service.ReviewAsync(new ReviewInput(id, ReviewOutcome.Approve, null, TestDb.Today), TestDb.Manager));
    }

    [Fact]
    public async Task Manager_notes_are_stored_separately_from_review_comments()
    {
        var id = await _service.StartAsync(1, TestDb.Applicant);
        await _service.SaveManagerNotesAsync(id, "  private note  ");

        var saved = await LoadAsync(id);
        Assert.Equal("private note", saved.ManagerNotes);
        Assert.Null(saved.ReviewComment);
    }
}
