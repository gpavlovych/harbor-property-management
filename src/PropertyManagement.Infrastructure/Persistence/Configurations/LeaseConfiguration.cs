using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> builder)
    {
        builder.Property(l => l.MonthlyRent).HasPrecision(10, 2);
        builder.Property(l => l.TenantId).HasMaxLength(450).IsRequired();
        builder.HasIndex(l => new { l.UnitId, l.StartDate, l.EndDate });

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
