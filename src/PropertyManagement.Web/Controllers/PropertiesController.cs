using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Catalog;
using PropertyManagement.Application.Common;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Web.Mvc;
using PropertyManagement.Web.ViewModels;

namespace PropertyManagement.Web.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class PropertiesController(ICatalogService catalog) : Controller
{
    private const string ListTarget = "#property-list";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await catalog.GetPropertiesAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var property = await catalog.GetPropertyAsync(id, ct);
        return property is null ? NotFound() : View(property);
    }

    // ----- modals -----

    [HttpGet]
    public IActionResult Create() => PartialView("_PropertyForm", new PropertyFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PropertyFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return PartialView("_PropertyForm", form);

        await catalog.CreatePropertyAsync(form.ToInput(), ct);
        return this.ModalRefresh(ListTarget, "_PropertyList", await catalog.GetPropertiesAsync(ct), $"Property '{form.Name}' was added.");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, string mode = "list", CancellationToken ct = default)
    {
        var property = await catalog.GetPropertyAsync(id, ct);
        return property is null ? NotFound() : PartialView("_PropertyForm", PropertyFormViewModel.From(property, mode));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PropertyFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return PartialView("_PropertyForm", form);

        try
        {
            await catalog.UpdatePropertyAsync(form.ToInput(), ct);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }

        return form.Mode == "details"
            ? this.ModalRedirect(Url.Action(nameof(Details), new { id = form.Id })!, "Property updated.")
            : this.ModalRefresh(ListTarget, "_PropertyList", await catalog.GetPropertiesAsync(ct), "Property updated.");
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, string mode = "list", CancellationToken ct = default)
    {
        var property = await catalog.GetPropertyAsync(id, ct);
        if (property is null) return NotFound();

        return PartialView("_ConfirmDelete", new ConfirmDeleteViewModel
        {
            Id = id,
            Title = "Remove property",
            Message = $"Remove '{property.Name}' and all of its units? This cannot be undone.",
            Action = nameof(Delete),
            Controller = "Properties",
            Mode = mode
        });
    }

    [HttpPost, ValidateAntiForgeryToken, ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(int id, string mode = "list", CancellationToken ct = default)
    {
        try
        {
            await catalog.DeletePropertyAsync(id, ct);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return PartialView("_ConfirmDelete", new ConfirmDeleteViewModel
            {
                Id = id, Title = "Remove property", Message = string.Empty, Action = nameof(Delete), Controller = "Properties", Mode = mode
            });
        }

        return mode == "details"
            ? this.ModalRedirect(Url.Action(nameof(Index))!, "Property removed.")
            : this.ModalRefresh(ListTarget, "_PropertyList", await catalog.GetPropertiesAsync(ct), "Property removed.");
    }
}
