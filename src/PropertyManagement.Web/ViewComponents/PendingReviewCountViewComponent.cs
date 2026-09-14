using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Applications;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>Navbar badge showing how many applications are waiting for review.</summary>
public class PendingReviewCountViewComponent(IApplicationQueryService queries) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await queries.CountPendingReviewAsync(HttpContext.RequestAborted));
}
