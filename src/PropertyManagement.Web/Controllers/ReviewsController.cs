using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Applications;
using PropertyManagement.Application.Applications.Models;
using PropertyManagement.Application.Common;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Web.Mvc;
using PropertyManagement.Web.ViewModels;

namespace PropertyManagement.Web.Controllers;

/// <summary>The review modal. Approve, Return or Deny a submitted application.</summary>
[Authorize(Roles = Roles.PropertyManager)]
public class ReviewsController(IRentalApplicationService applications, IApplicationQueryService queries, TimeProvider time) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(int applicationId, CancellationToken ct)
    {
        var details = await queries.GetDetailsAsync(applicationId, User.UserId(), isManager: true, null, ct);
        if (details is null) return NotFound();
        if (!details.CanReview) return Forbid();

        return PartialView("_ReviewForm", new ReviewViewModel
        {
            ApplicationId = applicationId,
            ApplicantName = details.ApplicantName,
            UnitLabel = ApplicationPageViewModel.From(details).UnitLabel,
            LeaseStartDate = DateOnly.FromDateTime(time.GetLocalNow().DateTime).AddDays(1)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReviewViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await applications.ReviewAsync(model.ToInput(), User.UserId(), ct);
                var details = await queries.GetDetailsAsync(model.ApplicationId, User.UserId(), isManager: true, ApplicationSection.Summary, ct);
                var message = model.Outcome switch
                {
                    ReviewOutcome.Approve => "Application approved and a twelve-month lease was issued.",
                    ReviewOutcome.Return => "Application returned to the applicant.",
                    _ => "Application denied."
                };
                return this.ModalRefresh(ApplicationsController.PageTarget, ApplicationsController.PagePartial, ApplicationPageViewModel.From(details!), message);
            }
            catch (EntityNotFoundException) { return NotFound(); }
            catch (DomainException ex) { ModelState.AddDomainError(ex); }
        }

        var current = await queries.GetDetailsAsync(model.ApplicationId, User.UserId(), isManager: true, null, ct);
        if (current is not null)
        {
            model.ApplicantName = current.ApplicantName;
            model.UnitLabel = ApplicationPageViewModel.From(current).UnitLabel;
        }
        return PartialView("_ReviewForm", model);
    }
}
