using System.ComponentModel.DataAnnotations;
using PropertyManagement.Application.Common;

namespace PropertyManagement.Web.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, Display(Name = "I am a")]
    public string Role { get; set; } = Roles.Applicant;

    public static readonly (string Value, string Label)[] RoleOptions =
    [
        (Roles.Applicant, "Applicant – I want to apply for a unit"),
        (Roles.PropertyManager, "Property Manager – I manage properties and review applications")
    ];
}
