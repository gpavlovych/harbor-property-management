using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Application.Common.Interfaces;

/// <summary>
/// Persistence port for the application layer. Implemented by the EF Core context in Infrastructure.
/// Identity users are exposed only as a read-only projection so the application never depends on ASP.NET Identity.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Property> Properties { get; }
    DbSet<Unit> Units { get; }
    DbSet<UnitType> UnitTypes { get; }
    DbSet<RentalApplication> RentalApplications { get; }
    DbSet<Residence> Residences { get; }
    DbSet<Lease> Leases { get; }
    DbSet<ApplicationStatusHistory> ApplicationStatusHistory { get; }
    IQueryable<UserSummary> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
