using System.ComponentModel.DataAnnotations;
using PropertyManagement.Application.Applications.Models;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Web.ViewModels;

/// <summary>Section 1 of the application. Validation rules are defined once, here.</summary>
public class ApplicantInformationViewModel
{
    [Required, StringLength(120), Display(Name = "Full name")]
    public string? FullName { get; set; }

    [Required, Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required, EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [Required, StringLength(300), Display(Name = "Current address")]
    public string? CurrentAddress { get; set; }

    public static ApplicantInformationViewModel From(ApplicantInformationInput i) => new()
    {
        FullName = i.FullName, Phone = i.Phone, Email = i.Email, CurrentAddress = i.CurrentAddress
    };

    public ApplicantInformationInput ToInput() => new(FullName, Phone, Email, CurrentAddress);
}

/// <summary>Form behind the residence modal. Maps to <see cref="ResidenceInput"/>.</summary>
public class ResidenceFormViewModel : IValidatableObject
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }

    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(120), Display(Name = "Landlord name")]
    public string LandlordName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30), Display(Name = "Landlord phone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [Required, DataType(DataType.Date), Display(Name = "Move-in date")]
    public DateOnly? MoveInDate { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Move-out date")]
    public DateOnly? MoveOutDate { get; set; }

    public bool IsNew => Id == 0;

    public static ResidenceFormViewModel From(ResidenceItem r) => new()
    {
        Id = r.Id, ApplicationId = r.ApplicationId, Address = r.Address, LandlordName = r.LandlordName,
        LandlordPhone = r.LandlordPhone, MoveInDate = r.MoveInDate, MoveOutDate = r.MoveOutDate
    };

    public ResidenceInput ToInput() => new(Id, ApplicationId, Address, LandlordName, LandlordPhone, MoveInDate!.Value, MoveOutDate!.Value);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MoveInDate.HasValue && MoveOutDate.HasValue && MoveOutDate <= MoveInDate)
            yield return new ValidationResult("Move-out date must be after the move-in date.", [nameof(MoveOutDate)]);
    }
}

/// <summary>One view model drives the single-page application; each section renders through its own partial or view component.</summary>
public class ApplicationPageViewModel
{
    public int Id { get; set; }
    public ApplicationStatus Status { get; set; }

    public string PropertyName { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public decimal MonthlyRent { get; set; }
    public bool UnitIsAvailable { get; set; }

    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;

    public ApplicationSection CurrentSection { get; set; }
    public bool IsEditable { get; set; }
    public bool IsOwner { get; set; }
    public bool IsManager { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanReview { get; set; }

    public bool ApplicantInformationCompleted { get; set; }
    public bool ResidenceHistoryCompleted { get; set; }
    public List<string> BlockingIssues { get; set; } = [];

    public ApplicantInformationViewModel ApplicantInformation { get; set; } = new();
    public ResidenceList Residences { get; set; } = new();

    public string? ReviewComment { get; set; }
    public string? ManagerNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public LeaseSummary? Lease { get; set; }

    public string UnitLabel => $"{PropertyName} · Unit {UnitNumber}";

    public static ApplicationPageViewModel From(ApplicationDetails d) => new()
    {
        Id = d.Id,
        Status = d.Status,
        PropertyName = d.PropertyName,
        PropertyAddress = d.PropertyAddress,
        UnitNumber = d.UnitNumber,
        UnitType = d.UnitType,
        Bedrooms = d.Bedrooms,
        MonthlyRent = d.MonthlyRent,
        UnitIsAvailable = d.UnitIsAvailable,
        ApplicantName = d.ApplicantName,
        ApplicantEmail = d.ApplicantEmail,
        CurrentSection = d.CurrentSection,
        IsEditable = d.IsEditable,
        IsOwner = d.IsOwner,
        IsManager = d.IsManager,
        CanSubmit = d.CanSubmit,
        CanWithdraw = d.CanWithdraw,
        CanReview = d.CanReview,
        ApplicantInformationCompleted = d.ApplicantInformationCompleted,
        ResidenceHistoryCompleted = d.ResidenceHistoryCompleted,
        BlockingIssues = d.BlockingIssues,
        ApplicantInformation = ApplicantInformationViewModel.From(d.ApplicantInformation),
        Residences = d.Residences,
        ReviewComment = d.ReviewComment,
        ManagerNotes = d.ManagerNotes,
        CreatedAt = d.CreatedAt,
        SubmittedAt = d.SubmittedAt,
        ReviewedAt = d.ReviewedAt,
        Lease = d.Lease
    };
}

/// <summary>What the single application form posts. The button clicked sets <see cref="Command"/>.</summary>
public class ApplicationSectionPostModel
{
    public int Id { get; set; }
    public ApplicationSection CurrentSection { get; set; }
    public string Command { get; set; } = string.Empty;
    public ApplicantInformationViewModel ApplicantInformation { get; set; } = new();

    public const string Continue = "continue";
    public const string Back = "back";
    public const string Submit = "submit";
}

public class WithdrawViewModel
{
    public int ApplicationId { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }
}

/// <summary>Form behind the review modal. Maps to <see cref="ReviewInput"/>.</summary>
public class ReviewViewModel : IValidatableObject
{
    public int ApplicationId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string UnitLabel { get; set; } = string.Empty;

    [Required]
    public ReviewOutcome? Outcome { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }

    [DataType(DataType.Date), Display(Name = "Lease start date")]
    public DateOnly? LeaseStartDate { get; set; }

    public ReviewInput ToInput() => new(ApplicationId, Outcome!.Value, Comment, LeaseStartDate);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Outcome is null) yield break;
        if (ApplicationWorkflow.RequiresComment(Outcome.Value) && string.IsNullOrWhiteSpace(Comment))
            yield return new ValidationResult($"A comment is required when you {Outcome.Value.ToString().ToLowerInvariant()} an application.", [nameof(Comment)]);
        if (Outcome == ReviewOutcome.Approve && LeaseStartDate is null)
            yield return new ValidationResult("A lease start date is required to approve.", [nameof(LeaseStartDate)]);
    }
}

public class ManagerNotesViewModel
{
    public int ApplicationId { get; set; }

    [StringLength(4000)]
    public string? Notes { get; set; }
}
