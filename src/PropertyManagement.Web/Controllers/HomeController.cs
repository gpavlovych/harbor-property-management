using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Catalog;
using PropertyManagement.Application.Catalog.Models;
using PropertyManagement.Web.Mvc;
using PropertyManagement.Web.ViewModels;

namespace PropertyManagement.Web.Controllers;

public class HomeController(ICatalogService catalog) : Controller
{
    /// <summary>Browse available units. Open to everyone; applicants can start an application from here.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(UnitBrowseFilter filter, CancellationToken ct)
    {
        var applicantId = User.Identity?.IsAuthenticated == true && !User.IsPropertyManager() ? User.UserId() : null;
        return View(await catalog.BrowseUnitsAsync(filter, applicantId, ct));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
