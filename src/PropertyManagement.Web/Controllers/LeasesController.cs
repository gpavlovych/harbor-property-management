using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Applications;
using PropertyManagement.Web.Mvc;

namespace PropertyManagement.Web.Controllers;

[Authorize]
public class LeasesController(IApplicationQueryService queries) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var restrictTo = User.IsPropertyManager() ? null : User.UserId();
        return View(await queries.ListLeasesAsync(restrictTo, ct));
    }
}
