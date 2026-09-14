using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Catalog;
using PropertyManagement.Application.Common;
using PropertyManagement.Domain.Exceptions;
using PropertyManagement.Web.Mvc;
using PropertyManagement.Web.ViewModels;

namespace PropertyManagement.Web.Controllers;

/// <summary>Unit CRUD. Every action renders a modal partial; success refreshes the property's units table.</summary>
[Authorize(Roles = Roles.PropertyManager)]
public class UnitsController(ICatalogService catalog) : Controller
{
    private const string TableTarget = "#units-table";
    private const string TableComponent = "UnitsTable";

    [HttpGet]
    public async Task<IActionResult> Create(int propertyId, CancellationToken ct)
    {
        if (await catalog.GetPropertyAsync(propertyId, ct) is null) return NotFound();
        var form = new UnitFormViewModel { PropertyId = propertyId, UnitTypeOptions = await catalog.GetUnitTypeOptionsAsync(null, ct) };
        return PartialView("_UnitForm", form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UnitFormViewModel form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await catalog.CreateUnitAsync(form.ToInput(), ct);
                return this.ModalRefreshComponent(TableTarget, TableComponent, new { propertyId = form.PropertyId }, $"Unit {form.UnitNumber} was added.");
            }
            catch (EntityNotFoundException) { return NotFound(); }
            catch (DomainException ex) { ModelState.AddDomainError(ex); }
        }

        form.UnitTypeOptions = await catalog.GetUnitTypeOptionsAsync(null, ct);
        return PartialView("_UnitForm", form);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var unit = await catalog.GetUnitAsync(id, ct);
        if (unit is null) return NotFound();

        var form = UnitFormViewModel.From(unit);
        form.UnitTypeOptions = await catalog.GetUnitTypeOptionsAsync(unit.UnitTypeId, ct);
        return PartialView("_UnitForm", form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UnitFormViewModel form, CancellationToken ct)
    {
        var unit = await catalog.GetUnitAsync(form.Id, ct);
        if (unit is null) return NotFound();
        form.PropertyId = unit.PropertyId; // never trust the posted property

        if (ModelState.IsValid)
        {
            try
            {
                await catalog.UpdateUnitAsync(form.ToInput(), ct);
                return this.ModalRefreshComponent(TableTarget, TableComponent, new { propertyId = unit.PropertyId }, $"Unit {form.UnitNumber} was updated.");
            }
            catch (DomainException ex) { ModelState.AddDomainError(ex); }
        }

        form.UnitTypeOptions = await catalog.GetUnitTypeOptionsAsync(unit.UnitTypeId, ct);
        return PartialView("_UnitForm", form);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var unit = await catalog.GetUnitAsync(id, ct);
        if (unit is null) return NotFound();

        return PartialView("_ConfirmDelete", new ConfirmDeleteViewModel
        {
            Id = id,
            Title = "Remove unit",
            Message = $"Remove unit {unit.UnitNumber} from {unit.PropertyName}?",
            Action = nameof(Delete),
            Controller = "Units"
        });
    }

    [HttpPost, ValidateAntiForgeryToken, ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        var unit = await catalog.GetUnitAsync(id, ct);
        if (unit is null) return NotFound();

        try
        {
            await catalog.DeleteUnitAsync(id, ct);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return PartialView("_ConfirmDelete", new ConfirmDeleteViewModel
            {
                Id = id, Title = "Remove unit", Message = string.Empty, Action = nameof(Delete), Controller = "Units"
            });
        }

        return this.ModalRefreshComponent(TableTarget, TableComponent, new { propertyId = unit.PropertyId }, $"Unit {unit.UnitNumber} was removed.");
    }
}
