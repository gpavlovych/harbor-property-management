using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Applications;
using PropertyManagement.Application.Applications.Models;
using PropertyManagement.Application.Common;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Web.Mvc;
using PropertyManagement.Web.ViewModels;

namespace PropertyManagement.Web.Controllers;

[Authorize]
public class ApplicationsController(IRentalApplicationService applications, IApplicationQueryService queries) : Controller
{
    public const string PageTarget = "#application-page";
    public const string PagePartial = "~/Views/Applications/_ApplicationPage.cshtml";

    /// <summary>Applicants see their own applications; property managers see all of them. Filtering and paging run in SQL.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(ApplicationListFilter filter, CancellationToken ct)
    {
        var restrictTo = User.IsPropertyManager() ? null : User.UserId();
        return View(await queries.ListAsync(filter, restrictTo, ct));
    }

    [HttpPost, Authorize(Roles = Roles.Applicant), ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int unitId, CancellationToken ct)
    {
        try
        {
            var id = await applications.StartAsync(unitId, User.UserId(), ct);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (EntityNotFoundException) { return NotFound(); }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    /// <summary>The single application page. One section is shown at a time.</summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id, ApplicationSection? section, CancellationToken ct)
    {
        var details = await queries.GetDetailsAsync(id, User.UserId(), User.IsPropertyManager(), section, ct);
        return details is null ? NotFound() : View(ApplicationPageViewModel.From(details));
    }

    /// <summary>The one action the application form posts to. The button clicked sets <c>Command</c>.</summary>
    [HttpPost, Authorize(Roles = Roles.Applicant), ValidateAntiForgeryToken]
    public async Task<IActionResult> Section(ApplicationSectionPostModel post, CancellationToken ct)
    {
        var userId = User.UserId();

        return post.Command switch
        {
            // Back never saves.
            ApplicationSectionPostModel.Back => RedirectToDetails(post.Id, Previous(post.CurrentSection)),
            ApplicationSectionPostModel.Submit => await SubmitAsync(post.Id, userId, ct),
            ApplicationSectionPostModel.Continue => await ContinueAsync(post, userId, ct),
            _ => BadRequest("Unknown command.")
        };
    }

    private async Task<IActionResult> ContinueAsync(ApplicationSectionPostModel post, string userId, CancellationToken ct)
    {
        var details = await queries.GetDetailsAsync(post.Id, userId, isManager: false, post.CurrentSection, ct);
        if (details is null) return NotFound();
        if (!details.IsEditable) return Forbid(); // posts are rejected unless the application is Draft or Returned

        var page = ApplicationPageViewModel.From(details);
        try
        {
            switch (post.CurrentSection)
            {
                case ApplicationSection.ApplicantInformation:
                    ModelState.KeepOnly(nameof(post.ApplicantInformation));
                    if (!ModelState.IsValid) return RenderWithPostedValues(page, post);
                    await applications.SaveApplicantInformationAsync(post.Id, userId, post.ApplicantInformation.ToInput(), ct);
                    return RedirectToDetails(post.Id, ApplicationSection.ResidenceHistory);

                case ApplicationSection.ResidenceHistory:
                    await applications.CompleteResidenceHistoryAsync(post.Id, userId, ct);
                    return RedirectToDetails(post.Id, ApplicationSection.Summary);

                default:
                    return BadRequest("The summary has nothing to continue to.");
            }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DomainException ex)
        {
            ModelState.AddDomainError(ex, nameof(post.ApplicantInformation));
            return RenderWithPostedValues(page, post);
        }
    }

    private async Task<IActionResult> SubmitAsync(int id, string userId, CancellationToken ct)
    {
        try
        {
            await applications.SubmitAsync(id, userId, ct);
            TempData["Success"] = "Your application has been submitted for review.";
        }
        catch (EntityNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DomainException ex) { TempData["Error"] = ex.Message; }

        return RedirectToDetails(id, ApplicationSection.Summary);
    }

    private IActionResult RenderWithPostedValues(ApplicationPageViewModel page, ApplicationSectionPostModel post)
    {
        if (post.CurrentSection == ApplicationSection.ApplicantInformation)
        {
            page.ApplicantInformation = post.ApplicantInformation;
        }
        return View(nameof(Details), page);
    }

    private IActionResult RedirectToDetails(int id, ApplicationSection section) =>
        RedirectToAction(nameof(Details), new { id, section });

    private static ApplicationSection Previous(ApplicationSection section) => section switch
    {
        ApplicationSection.Summary => ApplicationSection.ResidenceHistory,
        _ => ApplicationSection.ApplicantInformation
    };

    // ----- withdraw modal -----

    [HttpGet, Authorize(Roles = Roles.Applicant)]
    public async Task<IActionResult> Withdraw(int id, CancellationToken ct)
    {
        var details = await queries.GetDetailsAsync(id, User.UserId(), isManager: false, null, ct);
        if (details is null) return NotFound();
        if (!details.CanWithdraw) return Forbid();
        return PartialView("_WithdrawForm", new WithdrawViewModel { ApplicationId = id });
    }

    [HttpPost, Authorize(Roles = Roles.Applicant), ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(WithdrawViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return PartialView("_WithdrawForm", model);

        try
        {
            await applications.WithdrawAsync(model.ApplicationId, User.UserId(), model.Comment, ct);
        }
        catch (EntityNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return PartialView("_WithdrawForm", model);
        }

        var details = await queries.GetDetailsAsync(model.ApplicationId, User.UserId(), isManager: false, ApplicationSection.Summary, ct);
        return this.ModalRefresh(PageTarget, PagePartial, ApplicationPageViewModel.From(details!), "Your application has been withdrawn.");
    }

    // ----- property manager notes modal (never rendered to applicants) -----

    [HttpGet, Authorize(Roles = Roles.PropertyManager)]
    public async Task<IActionResult> Notes(int id, CancellationToken ct)
    {
        var details = await queries.GetDetailsAsync(id, User.UserId(), isManager: true, null, ct);
        if (details is null) return NotFound();
        return PartialView("_ManagerNotesForm", new ManagerNotesViewModel { ApplicationId = id, Notes = details.ManagerNotes });
    }

    [HttpPost, Authorize(Roles = Roles.PropertyManager), ValidateAntiForgeryToken]
    public async Task<IActionResult> Notes(ManagerNotesViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return PartialView("_ManagerNotesForm", model);

        try
        {
            await applications.SaveManagerNotesAsync(model.ApplicationId, model.Notes, ct);
        }
        catch (EntityNotFoundException) { return NotFound(); }

        var details = await queries.GetDetailsAsync(model.ApplicationId, User.UserId(), isManager: true, null, ct);
        return this.ModalRefresh("#manager-notes", "_ManagerNotes", ApplicationPageViewModel.From(details!), "Notes saved.");
    }
}
