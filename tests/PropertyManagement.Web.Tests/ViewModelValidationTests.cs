using System.ComponentModel.DataAnnotations;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Web.ViewModels;

namespace PropertyManagement.Web.Tests;

public class ViewModelValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData(ReviewOutcome.Return)]
    [InlineData(ReviewOutcome.Deny)]
    public void Return_and_Deny_require_a_comment(ReviewOutcome outcome)
    {
        var errors = Validate(new ReviewViewModel { ApplicationId = 1, Outcome = outcome, Comment = "  " });
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ReviewViewModel.Comment)));
    }

    [Fact]
    public void Approve_requires_a_lease_start_date_but_not_a_comment()
    {
        var errors = Validate(new ReviewViewModel { ApplicationId = 1, Outcome = ReviewOutcome.Approve, Comment = null, LeaseStartDate = null });
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ReviewViewModel.LeaseStartDate)));
        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(ReviewViewModel.Comment)));
    }

    [Fact]
    public void Outcome_is_required()
    {
        var errors = Validate(new ReviewViewModel { ApplicationId = 1 });
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ReviewViewModel.Outcome)));
    }

    [Fact]
    public void Residence_move_out_must_follow_move_in()
    {
        var form = new ResidenceFormViewModel
        {
            ApplicationId = 1, Address = "x", LandlordName = "y", LandlordPhone = "555",
            MoveInDate = new DateOnly(2024, 1, 1), MoveOutDate = new DateOnly(2023, 1, 1)
        };
        Assert.Contains(Validate(form), e => e.MemberNames.Contains(nameof(ResidenceFormViewModel.MoveOutDate)));
    }

    [Fact]
    public void Applicant_information_fields_are_all_required()
    {
        var errors = Validate(new ApplicantInformationViewModel());
        Assert.Equal(4, errors.Count);
    }

    [Fact]
    public void Form_view_models_map_to_application_inputs()
    {
        var unit = new UnitFormViewModel { Id = 5, PropertyId = 2, UnitNumber = "3B", Bedrooms = 2, MonthlyRent = 1900, UnitTypeId = 4 }.ToInput();
        Assert.Equal((5, 2, "3B", 2, 1900m, 4), (unit.Id, unit.PropertyId, unit.UnitNumber, unit.Bedrooms, unit.MonthlyRent, unit.UnitTypeId));

        var review = new ReviewViewModel { ApplicationId = 9, Outcome = ReviewOutcome.Approve, LeaseStartDate = new DateOnly(2026, 10, 1) }.ToInput();
        Assert.Equal(9, review.ApplicationId);
        Assert.Equal(ReviewOutcome.Approve, review.Outcome);
        Assert.Equal(new DateOnly(2026, 10, 1), review.LeaseStartDate);
    }
}
