using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

/// <summary>Audit entry recorded on every status change and review outcome.</summary>
public class ApplicationStatusHistory
{
    public int Id { get; set; }
    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;
    public ApplicationStatus? FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }
    /// <summary>Identity user id of the person who made the change.</summary>
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Comment { get; set; }
}
