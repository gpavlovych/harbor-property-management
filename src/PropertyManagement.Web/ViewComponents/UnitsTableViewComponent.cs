using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Catalog;
using PropertyManagement.Web.ViewModels;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>The units table on the property details page. Re-rendered after every unit modal succeeds.</summary>
public class UnitsTableViewComponent(ICatalogService catalog) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int propertyId)
    {
        var units = await catalog.GetUnitsForPropertyAsync(propertyId, HttpContext.RequestAborted);
        return View(new UnitsTableViewModel { PropertyId = propertyId, Units = units });
    }
}
