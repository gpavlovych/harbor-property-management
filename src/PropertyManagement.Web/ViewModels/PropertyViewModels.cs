using System.ComponentModel.DataAnnotations;
using PropertyManagement.Application.Catalog.Models;

namespace PropertyManagement.Web.ViewModels;

/// <summary>Form behind the add/edit property modal. Maps to <see cref="PropertyInput"/>.</summary>
public class PropertyFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "Street address")]
    public string StreetAddress { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(2, MinimumLength = 2), RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Use the two-letter state code.")]
    public string State { get; set; } = string.Empty;

    [Required, StringLength(10), Display(Name = "Postal code")]
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Where the modal should send the browser on success: "list" refreshes the list, "details" reloads the details page.</summary>
    public string Mode { get; set; } = "list";

    public bool IsNew => Id == 0;

    public static PropertyFormViewModel From(PropertyDetail p, string mode) => new()
    {
        Id = p.Id, Name = p.Name, StreetAddress = p.StreetAddress, City = p.City, State = p.State, PostalCode = p.PostalCode, Mode = mode
    };

    public PropertyInput ToInput() => new(Id, Name, StreetAddress, City, State, PostalCode);
}

public class ConfirmDeleteViewModel
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Action { get; init; }
    public required string Controller { get; init; }
    public string? Mode { get; init; }
}
