using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Domain.Entities;

public class Unit
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public int UnitTypeId { get; set; }
    public UnitType UnitType { get; set; } = null!;
    public string UnitNumber { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public decimal MonthlyRent { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<RentalApplication> Applications { get; set; } = new List<RentalApplication>();
    public ICollection<Lease> Leases { get; set; } = new List<Lease>();

    /// <summary>Requires <see cref="Leases"/> to be loaded.</summary>
    public bool IsAvailableOn(DateOnly date) => LeaseRules.IsUnitAvailableOn(Leases, date);
}
