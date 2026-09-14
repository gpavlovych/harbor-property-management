using System.ComponentModel.DataAnnotations;
using PropertyManagement.Application.Catalog.Models;
using PropertyManagement.Application.Common.Models;

namespace PropertyManagement.Web.ViewModels;

/// <summary>Form behind the add/edit unit modal. Maps to <see cref="UnitInput"/>.</summary>
public class UnitFormViewModel
{
    public int Id { get; set; }
    public int PropertyId { get; set; }

    [Required, StringLength(20), Display(Name = "Unit number")]
    public string UnitNumber { get; set; } = string.Empty;

    [Range(0, 20)]
    public int Bedrooms { get; set; }

    [Range(typeof(decimal), "1", "1000000"), Display(Name = "Monthly rent")]
    public decimal MonthlyRent { get; set; }

    [Required(ErrorMessage = "Please choose a unit type."), Range(1, int.MaxValue, ErrorMessage = "Please choose a unit type."), Display(Name = "Unit type")]
    public int UnitTypeId { get; set; }

    public List<LookupItem> UnitTypeOptions { get; set; } = [];

    public bool IsNew => Id == 0;

    public static UnitFormViewModel From(UnitDetail u) => new()
    {
        Id = u.Id, PropertyId = u.PropertyId, UnitNumber = u.UnitNumber, Bedrooms = u.Bedrooms, MonthlyRent = u.MonthlyRent, UnitTypeId = u.UnitTypeId
    };

    public UnitInput ToInput() => new(Id, PropertyId, UnitNumber, Bedrooms, MonthlyRent, UnitTypeId);
}

public class UnitsTableViewModel
{
    public int PropertyId { get; init; }
    public List<UnitRow> Units { get; init; } = [];
}
