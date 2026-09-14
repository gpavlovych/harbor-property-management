using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Web.Mvc;

/// <summary>
/// Conventions shared with wwwroot/js/site.js. A modal form posts with fetch; the response headers tell the page
/// what to do: re-render the modal (validation errors), refresh a page fragment, or navigate.
/// </summary>
public static class ModalControllerExtensions
{
    public const string ResultHeader = "X-Modal-Result";
    public const string TargetHeader = "X-Modal-Target";
    public const string LocationHeader = "X-Modal-Location";
    public const string MessageHeader = "X-Modal-Message";

    /// <summary>Close the modal and replace the element matching <paramref name="targetSelector"/> with the rendered partial.</summary>
    public static PartialViewResult ModalRefresh(this Controller controller, string targetSelector, string partialViewName, object? model, string? message = null)
    {
        controller.Response.Headers[ResultHeader] = "refresh";
        controller.Response.Headers[TargetHeader] = targetSelector;
        if (message is not null) controller.Response.Headers[MessageHeader] = Uri.EscapeDataString(message);
        return controller.PartialView(partialViewName, model);
    }

    /// <summary>Close the modal and replace the target with a view component's output.</summary>
    public static ViewComponentResult ModalRefreshComponent(this Controller controller, string targetSelector, string componentName, object arguments, string? message = null)
    {
        controller.Response.Headers[ResultHeader] = "refresh";
        controller.Response.Headers[TargetHeader] = targetSelector;
        if (message is not null) controller.Response.Headers[MessageHeader] = Uri.EscapeDataString(message);
        return controller.ViewComponent(componentName, arguments);
    }

    /// <summary>Close the modal and navigate the browser to <paramref name="url"/>.</summary>
    public static IActionResult ModalRedirect(this Controller controller, string url, string? message = null)
    {
        controller.Response.Headers[ResultHeader] = "redirect";
        controller.Response.Headers[LocationHeader] = url;
        if (message is not null) controller.TempData["Success"] = message;
        return controller.Ok();
    }
}
