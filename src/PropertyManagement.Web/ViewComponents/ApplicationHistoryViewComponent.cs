using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Applications;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>Status changes and review outcomes (who, when, comment). Rendered for property managers only.</summary>
public class ApplicationHistoryViewComponent(IApplicationQueryService queries) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int applicationId) =>
        View(await queries.GetHistoryAsync(applicationId, HttpContext.RequestAborted));
}
