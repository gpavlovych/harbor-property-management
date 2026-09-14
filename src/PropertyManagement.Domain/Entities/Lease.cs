namespace PropertyManagement.Domain.Entities;

public class Lease
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;
    /// <summary>Identity user id of the tenant. The user itself lives outside the domain.</summary>
    public string TenantId { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public DateTime CreatedAt { get; set; }
}
