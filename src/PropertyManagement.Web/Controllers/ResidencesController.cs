using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Applications;
using PropertyManagement.Application.Common;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Web.Mvc;
using PropertyManagement.Web.ViewModels;

namespace PropertyManagement.Web.Controllers;

/// <summary>Residences are added, edited and removed through a modal; success refreshes the residence list.</summary>
[Authorize(Roles = Roles.Applicant)]
public class ResidencesController(IRentalApplicationService applications, IApplicationQueryService queries) : Controller
{
    private const string ListTarget = "#residence-list";
    private const string ListPartial = "~/Views/Applications/_ResidenceList.cshtml";

    [HttpGet]
    public IActionResult Create(int applicationId) =>
        PartialView("_ResidenceForm", new ResidenceFormViewModel { ApplicationId = applicationId });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ResidenceFormViewModel form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await applications.AddResidenceAsync(User.UserId(), form.ToInput(), ct);
                return await RefreshListAsync(form.ApplicationId, "Residence added.", ct);
            }
            catch (EntityNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (DomainException ex) { ModelState.AddDomainError(ex); }
        }
        return PartialView("_ResidenceForm", form);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int applicationId, int id, CancellationToken ct)
    {
        var residence = await queries.GetResidenceAsync(applicationId, id, User.UserId(), ct);
        return residence is null ? NotFound() : PartialView("_ResidenceForm", ResidenceFormViewModel.From(residence));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ResidenceFormViewModel form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await applications.UpdateResidenceAsync(User.UserId(), form.ToInput(), ct);
                return await RefreshListAsync(form.ApplicationId, "Residence updated.", ct);
            }
            catch (EntityNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (DomainException ex) { ModelState.AddDomainError(ex); }
        }
        return PartialView("_ResidenceForm", form);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int applicationId, int id, CancellationToken ct)
    {
        var residence = await queries.GetResidenceAsync(applicationId, id, User.UserId(), ct);
        if (residence is null) return NotFound();

        return PartialView("_ConfirmDelete", new ConfirmDeleteViewModel
        {
            Id = id,
            Title = "Remove residence",
            Message = $"Remove {residence.Address} from your residence history?",
            Action = nameof(Delete),
            Controller = "Residences",
            Mode = applicationId.ToString()
        });
    }

    [HttpPost, ValidateAntiForgeryToken, ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(int id, string mode, CancellationToken ct)
    {
        if (!int.TryParse(mode, out var applicationId)) return BadRequest();

        try
        {
            await applications.RemoveResidenceAsync(applicationId, id, User.UserId(), ct);
        }
        catch (EntityNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return PartialView("_ConfirmDelete", new ConfirmDeleteViewModel
            {
                Id = id, Title = "Remove residence", Message = string.Empty, Action = nameof(Delete), Controller = "Residences", Mode = mode
            });
        }

        return await RefreshListAsync(applicationId, "Residence removed.", ct);
    }

    private async Task<IActionResult> RefreshListAsync(int applicationId, string message, CancellationToken ct)
    {
        var list = await queries.GetResidenceListAsync(applicationId, User.UserId(), isManager: false, ct);
        return list is null ? NotFound() : this.ModalRefresh(ListTarget, ListPartial, list, message);
    }
}
