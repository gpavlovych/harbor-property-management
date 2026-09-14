using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PropertyManagement.Application.Common;
using PropertyManagement.Domain.Exceptions;

namespace PropertyManagement.Web.Mvc;

public static class ControllerExtensions
{
    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("No signed-in user.");

    public static bool IsPropertyManager(this ClaimsPrincipal user) => user.IsInRole(Roles.PropertyManager);

    /// <summary>Attaches a domain rule violation to the model state, on the member it names when there is one.</summary>
    public static void AddDomainError(this ModelStateDictionary modelState, DomainException ex, string? prefix = null)
    {
        var key = ex.MemberName is null ? string.Empty : (prefix is null ? ex.MemberName : $"{prefix}.{ex.MemberName}");
        modelState.AddModelError(key, ex.Message);
    }

    /// <summary>Keeps only the model-state entries that belong to the given prefix so one section validates at a time.</summary>
    public static void KeepOnly(this ModelStateDictionary modelState, string prefix)
    {
        foreach (var key in modelState.Keys.Where(k => !k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            modelState.Remove(key);
        }
    }
}
