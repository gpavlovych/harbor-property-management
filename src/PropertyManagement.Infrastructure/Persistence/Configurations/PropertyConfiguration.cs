using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(120).IsRequired();
        builder.Property(p => p.StreetAddress).HasMaxLength(200).IsRequired();
        builder.Property(p => p.City).HasMaxLength(80).IsRequired();
        builder.Property(p => p.State).HasMaxLength(2).IsRequired();
        builder.Property(p => p.PostalCode).HasMaxLength(10).IsRequired();
        builder.Ignore(p => p.FullAddress);

        builder.HasMany(p => p.Units).WithOne(u => u.Property).HasForeignKey(u => u.PropertyId).OnDelete(DeleteBehavior.Cascade);
    }
}
