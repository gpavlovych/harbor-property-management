using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public class ResidenceConfiguration : IEntityTypeConfiguration<Residence>
{
    public void Configure(EntityTypeBuilder<Residence> builder)
    {
        builder.Property(r => r.Address).HasMaxLength(300).IsRequired();
        builder.Property(r => r.LandlordName).HasMaxLength(120).IsRequired();
        builder.Property(r => r.LandlordPhone).HasMaxLength(30).IsRequired();
    }
}
