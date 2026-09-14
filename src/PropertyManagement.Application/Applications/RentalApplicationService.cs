using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Applications.Models;
using PropertyManagement.Application.Common.Interfaces;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Application.Applications;

public class RentalApplicationService(IApplicationDbContext db, TimeProvider time) : IRentalApplicationService
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    public async Task<int> StartAsync(int unitId, string applicantId, CancellationToken ct = default)
    {
        var openStatuses = ApplicationWorkflow.OpenStatuses;
        var existing = await db.RentalApplications
            .Where(a => a.UnitId == unitId && a.ApplicantId == applicantId && openStatuses.Contains(a.Status))
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(ct);
        if (existing.HasValue) return existing.Value;

        var unit = await db.Units.Include(u => u.Leases).FirstOrDefaultAsync(u => u.Id == unitId, ct)
            ?? throw new EntityNotFoundException("Unit not found.");
        if (!unit.IsAvailableOn(Today))
            throw new DomainException("This unit is not available.");

        var applicant = await db.Users.FirstOrDefaultAsync(u => u.Id == applicantId, ct)
            ?? throw new EntityNotFoundException("Applicant not found.");

        var application = RentalApplication.Start(unitId, applicantId, applicant.FullName, applicant.Email, applicant.Phone, UtcNow);
        db.RentalApplications.Add(application);
        await db.SaveChangesAsync(ct);
        return application.Id;
    }

    public async Task SaveApplicantInformationAsync(int applicationId, string applicantId, ApplicantInformationInput input, CancellationToken ct = default)
    {
        var application = await LoadOwnedEditableAsync(applicationId, applicantId, ct);

        if (string.IsNullOrWhiteSpace(input.FullName) || string.IsNullOrWhiteSpace(input.Phone) ||
            string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.CurrentAddress))
            throw new DomainException("All applicant information fields are required.");

        application.FullName = input.FullName.Trim();
        application.Phone = input.Phone.Trim();
        application.Email = input.Email.Trim();
        application.CurrentAddress = input.CurrentAddress.Trim();
        application.ApplicantInformationCompletedAt = UtcNow;
        application.UpdatedAt = UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task CompleteResidenceHistoryAsync(int applicationId, string applicantId, CancellationToken ct = default)
    {
        var application = await LoadOwnedEditableAsync(applicationId, applicantId, ct, includeResidences: true);

        if (application.Residences.Count == 0)
            throw new DomainException("Add at least one prior residence before continuing.");

        application.ResidenceHistoryCompletedAt = UtcNow;
        application.UpdatedAt = UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> AddResidenceAsync(string applicantId, ResidenceInput input, CancellationToken ct = default)
    {
        var application = await LoadOwnedEditableAsync(input.ApplicationId, applicantId, ct);
        ValidateResidenceDates(input);

        var residence = new Residence { RentalApplicationId = application.Id };
        Apply(input, residence);
        db.Residences.Add(residence);
        application.UpdatedAt = UtcNow;
        await db.SaveChangesAsync(ct);
        return residence.Id;
    }

    public async Task UpdateResidenceAsync(string applicantId, ResidenceInput input, CancellationToken ct = default)
    {
        var application = await LoadOwnedEditableAsync(input.ApplicationId, applicantId, ct, includeResidences: true);
        ValidateResidenceDates(input);

        var residence = application.Residences.FirstOrDefault(r => r.Id == input.Id)
            ?? throw new EntityNotFoundException("Residence not found.");
        Apply(input, residence);
        application.UpdatedAt = UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveResidenceAsync(int applicationId, int residenceId, string applicantId, CancellationToken ct = default)
    {
        var application = await LoadOwnedEditableAsync(applicationId, applicantId, ct, includeResidences: true);
        var residence = application.Residences.FirstOrDefault(r => r.Id == residenceId)
            ?? throw new EntityNotFoundException("Residence not found.");

        application.Residences.Remove(residence);
        db.Residences.Remove(residence);
        if (application.Residences.Count == 0)
        {
            // The section is no longer valid; the applicant must complete it again before submitting.
            application.ResidenceHistoryCompletedAt = null;
        }
        application.UpdatedAt = UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SubmitAsync(int applicationId, string applicantId, CancellationToken ct = default)
    {
        var application = await db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Leases)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct)
            ?? throw new EntityNotFoundException("Application not found.");
        application.EnsureOwnedBy(applicantId);

        if (!ApplicationWorkflow.CanSubmit(application.Status))
            throw new DomainException($"An application in status '{application.Status}' cannot be submitted.");
        if (!application.AllSectionsCompleted)
            throw new DomainException("Complete every section before submitting.");
        if (!application.Unit.IsAvailableOn(Today))
            throw new DomainException("This unit has an active lease and is no longer available.");

        var now = UtcNow;
        application.TransitionTo(ApplicationStatus.Submitted, applicantId, now, comment: null);
        application.SubmittedAt = now;
        application.ReviewComment = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task WithdrawAsync(int applicationId, string applicantId, string? comment, CancellationToken ct = default)
    {
        var application = await db.RentalApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
            ?? throw new EntityNotFoundException("Application not found.");
        application.EnsureOwnedBy(applicantId);

        if (!ApplicationWorkflow.CanWithdraw(application.Status))
            throw new DomainException($"An application in status '{application.Status}' cannot be withdrawn.");

        application.TransitionTo(ApplicationStatus.Withdrawn, applicantId, UtcNow, comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReviewAsync(ReviewInput input, string managerId, CancellationToken ct = default)
    {
        var application = await db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Leases)
            .FirstOrDefaultAsync(a => a.Id == input.ApplicationId, ct)
            ?? throw new EntityNotFoundException("Application not found.");

        if (!ApplicationWorkflow.CanReview(application.Status))
            throw new DomainException($"An application in status '{application.Status}' cannot be reviewed.");
        if (ApplicationWorkflow.RequiresComment(input.Outcome) && string.IsNullOrWhiteSpace(input.Comment))
            throw new DomainException("A comment is required for this outcome.", nameof(ReviewInput.Comment));

        var now = UtcNow;
        if (input.Outcome == ReviewOutcome.Approve)
        {
            if (input.LeaseStartDate is null)
                throw new DomainException("A lease start date is required to approve.", nameof(ReviewInput.LeaseStartDate));
            try
            {
                application.IssueLease(input.LeaseStartDate.Value, Today, now);
            }
            catch (DomainException ex)
            {
                throw new DomainException(ex.Message, nameof(ReviewInput.LeaseStartDate));
            }
        }

        application.TransitionTo(input.Outcome.ToStatus(), managerId, now, input.Comment);
        application.ReviewedAt = now;
        application.ReviewComment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveManagerNotesAsync(int applicationId, string? notes, CancellationToken ct = default)
    {
        var application = await db.RentalApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
            ?? throw new EntityNotFoundException("Application not found.");
        application.ManagerNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await db.SaveChangesAsync(ct);
    }

    private async Task<RentalApplication> LoadOwnedEditableAsync(int applicationId, string applicantId, CancellationToken ct, bool includeResidences = false)
    {
        IQueryable<RentalApplication> query = db.RentalApplications;
        if (includeResidences) query = query.Include(a => a.Residences);

        var application = await query.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
            ?? throw new EntityNotFoundException("Application not found.");
        application.EnsureOwnedBy(applicantId);
        application.EnsureEditable();
        return application;
    }

    private static void Apply(ResidenceInput input, Residence residence)
    {
        residence.Address = input.Address.Trim();
        residence.LandlordName = input.LandlordName.Trim();
        residence.LandlordPhone = input.LandlordPhone.Trim();
        residence.MoveInDate = input.MoveInDate;
        residence.MoveOutDate = input.MoveOutDate;
    }

    private void ValidateResidenceDates(ResidenceInput input)
    {
        if (input.MoveOutDate <= input.MoveInDate)
            throw new DomainException("Move-out date must be after the move-in date.", nameof(ResidenceInput.MoveOutDate));
        if (input.MoveOutDate > Today)
            throw new DomainException("Move-out date cannot be in the future.", nameof(ResidenceInput.MoveOutDate));
    }
}
