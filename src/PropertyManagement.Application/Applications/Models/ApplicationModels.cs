using PropertyManagement.Application.Common.Models;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Applications.Models;

/// <summary>The sections of the single-page application, in order.</summary>
public enum ApplicationSection
{
    ApplicantInformation = 1,
    ResidenceHistory = 2,
    Summary = 3
}

public enum ApplicationSort { Updated, Applicant, Property, Status }

public record ApplicantInformationInput(string? FullName, string? Phone, string? Email, string? CurrentAddress);

public record ResidenceInput(int Id, int ApplicationId, string Address, string LandlordName, string LandlordPhone, DateOnly MoveInDate, DateOnly MoveOutDate);

public record ReviewInput(int ApplicationId, ReviewOutcome Outcome, string? Comment, DateOnly? LeaseStartDate);

public class ResidenceItem
{
    public int Id { get; init; }
    public int ApplicationId { get; init; }
    public string Address { get; init; } = string.Empty;
    public string LandlordName { get; init; } = string.Empty;
    public string LandlordPhone { get; init; } = string.Empty;
    public DateOnly MoveInDate { get; init; }
    public DateOnly MoveOutDate { get; init; }
}

public class ResidenceList
{
    public int ApplicationId { get; init; }
    public bool IsEditable { get; init; }
    public List<ResidenceItem> Residences { get; init; } = [];
}

public class LeaseSummary
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal MonthlyRent { get; init; }
}

/// <summary>Everything the single application page needs, with the editability decision already made.</summary>
public class ApplicationDetails
{
    public int Id { get; init; }
    public ApplicationStatus Status { get; init; }

    public string PropertyName { get; init; } = string.Empty;
    public string PropertyAddress { get; init; } = string.Empty;
    public string UnitNumber { get; init; } = string.Empty;
    public string UnitType { get; init; } = string.Empty;
    public int Bedrooms { get; init; }
    public decimal MonthlyRent { get; init; }
    public bool UnitIsAvailable { get; init; }

    public string ApplicantName { get; init; } = string.Empty;
    public string ApplicantEmail { get; init; } = string.Empty;

    public ApplicationSection CurrentSection { get; init; }
    public bool IsEditable { get; init; }
    public bool IsOwner { get; init; }
    public bool IsManager { get; init; }
    public bool CanSubmit { get; init; }
    public bool CanWithdraw { get; init; }
    public bool CanReview { get; init; }

    public bool ApplicantInformationCompleted { get; init; }
    public bool ResidenceHistoryCompleted { get; init; }
    public List<string> BlockingIssues { get; init; } = [];

    public ApplicantInformationInput ApplicantInformation { get; init; } = new(null, null, null, null);
    public ResidenceList Residences { get; init; } = new();

    public string? ReviewComment { get; init; }
    /// <summary>Only populated for property managers.</summary>
    public string? ManagerNotes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public LeaseSummary? Lease { get; init; }
}

public record ApplicationListFilter
{
    public ApplicationStatus? Status { get; init; }
    public int? PropertyId { get; init; }
    public ApplicationSort Sort { get; init; } = ApplicationSort.Updated;
    public bool Desc { get; init; } = true;
    public int Page { get; init; } = 1;
}

public class ApplicationListItem
{
    public int Id { get; init; }
    public string ApplicantName { get; init; } = string.Empty;
    public string PropertyName { get; init; } = string.Empty;
    public string UnitNumber { get; init; } = string.Empty;
    public ApplicationStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
}

public class ApplicationListResult
{
    public required ApplicationListFilter Filter { get; init; }
    public required PagedResult<ApplicationListItem> Results { get; init; }
    public List<LookupItem> Properties { get; init; } = [];
}

public class HistoryEntry
{
    public ApplicationStatus? FromStatus { get; init; }
    public ApplicationStatus ToStatus { get; init; }
    public string ChangedBy { get; init; } = string.Empty;
    public DateTime ChangedAt { get; init; }
    public string? Comment { get; init; }
}

public class LeaseItem
{
    public int Id { get; init; }
    public int ApplicationId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string PropertyName { get; init; } = string.Empty;
    public string UnitNumber { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal MonthlyRent { get; init; }
    public bool IsActive { get; init; }
}
