namespace PropertyManagement.Domain.Entities;

/// <summary>Lookup. Inactive values remain visible on units that already use them but cannot be chosen for other units.</summary>
public class UnitType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<Unit> Units { get; set; } = new List<Unit>();

    /// <summary>An inactive type may only be kept by a unit that already uses it.</summary>
    public bool IsSelectableFor(int? currentUnitTypeId) => IsActive || Id == currentUnitTypeId;
}
