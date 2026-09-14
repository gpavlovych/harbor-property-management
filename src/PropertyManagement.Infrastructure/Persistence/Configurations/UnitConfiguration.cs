using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.Property(u => u.UnitNumber).HasMaxLength(20).IsRequired();
        builder.Property(u => u.MonthlyRent).HasPrecision(10, 2);
        builder.HasIndex(u => new { u.PropertyId, u.UnitNumber }).IsUnique();

        builder.HasOne(u => u.UnitType).WithMany(t => t.Units).HasForeignKey(u => u.UnitTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(u => u.Applications).WithOne(a => a.Unit).HasForeignKey(a => a.UnitId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(u => u.Leases).WithOne(l => l.Unit).HasForeignKey(l => l.UnitId).OnDelete(DeleteBehavior.Restrict);
    }
}
