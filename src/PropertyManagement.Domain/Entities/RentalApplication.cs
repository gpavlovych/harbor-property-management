using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Domain.Entities;

/// <summary>Aggregate root for a rental application. State changes go through <see cref="TransitionTo"/> so history is never skipped.</summary>
public class RentalApplication
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    /// <summary>Identity user id of the applicant. The user itself lives outside the domain.</summary>
    public string ApplicantId { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    // Section 1: applicant information
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? CurrentAddress { get; set; }

    // Section 2: residence history
    public ICollection<Residence> Residences { get; set; } = new List<Residence>();

    /// <summary>Set when the section was last saved in a valid state; both must be set before submission.</summary>
    public DateTime? ApplicantInformationCompletedAt { get; set; }
    public DateTime? ResidenceHistoryCompletedAt { get; set; }

    /// <summary>Latest review comment shown to the applicant (e.g. why the application was returned).</summary>
    public string? ReviewComment { get; set; }

    /// <summary>Private notes for property managers. Never rendered to applicants.</summary>
    public string? ManagerNotes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public Lease? Lease { get; set; }
    public ICollection<ApplicationStatusHistory> History { get; set; } = new List<ApplicationStatusHistory>();

    public bool IsEditable => ApplicationWorkflow.IsEditable(Status);
    public bool IsTerminal => ApplicationWorkflow.IsTerminal(Status);
    public bool AllSectionsCompleted => ApplicantInformationCompletedAt.HasValue && ResidenceHistoryCompletedAt.HasValue;

    public static RentalApplication Start(int unitId, string applicantId, string? fullName, string? email, string? phone, DateTime now)
    {
        var application = new RentalApplication
        {
            UnitId = unitId,
            ApplicantId = applicantId,
            Status = ApplicationStatus.Draft,
            FullName = fullName,
            Email = email,
            Phone = phone,
            CreatedAt = now,
            UpdatedAt = now
        };
        application.History.Add(new ApplicationStatusHistory
        {
            FromStatus = null, ToStatus = ApplicationStatus.Draft, ChangedByUserId = applicantId, ChangedAt = now, Comment = "Application started."
        });
        return application;
    }

    public void EnsureOwnedBy(string applicantId)
    {
        if (ApplicantId != applicantId)
            throw new UnauthorizedAccessException("You do not have access to this application.");
    }

    public void EnsureEditable()
    {
        if (!IsEditable)
            throw new DomainException($"An application in status '{Status}' cannot be edited.");
    }

    /// <summary>Moves to a new status, validating the transition and recording who did it and when.</summary>
    public void TransitionTo(ApplicationStatus to, string userId, DateTime at, string? comment)
    {
        if (!ApplicationWorkflow.CanTransition(Status, to))
            throw new DomainException($"Cannot move an application from '{Status}' to '{to}'.");

        History.Add(new ApplicationStatusHistory
        {
            FromStatus = Status,
            ToStatus = to,
            ChangedByUserId = userId,
            ChangedAt = at,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        });
        Status = to;
        UpdatedAt = at;
    }

    /// <summary>Issues the twelve-month lease that accompanies an approval. Requires <c>Unit.Leases</c> to be loaded.</summary>
    public Lease IssueLease(DateOnly startDate, DateOnly today, DateTime now)
    {
        if (LeaseRules.ConflictsWithExisting(Unit.Leases, today, startDate))
            throw new DomainException("This unit already has an active lease. The application cannot be approved.");

        Lease = new Lease
        {
            UnitId = UnitId,
            TenantId = ApplicantId,
            StartDate = startDate,
            EndDate = LeaseRules.EndDateFor(startDate),
            MonthlyRent = Unit.MonthlyRent,
            CreatedAt = now
        };
        return Lease;
    }
}
