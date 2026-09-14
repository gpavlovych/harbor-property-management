using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Applications.Models;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>Section 2 of the application. Renders the residence list in editable or read-only mode.</summary>
public class ResidenceHistorySectionViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ResidenceList residences, bool completed)
    {
        ViewData["Completed"] = completed;
        return View(residences);
    }
}
