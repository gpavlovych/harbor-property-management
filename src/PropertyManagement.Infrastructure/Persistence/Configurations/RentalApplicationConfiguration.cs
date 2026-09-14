using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public class RentalApplicationConfiguration : IEntityTypeConfiguration<RentalApplication>
{
    public void Configure(EntityTypeBuilder<RentalApplication> builder)
    {
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => a.Status);
        builder.Property(a => a.ApplicantId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.FullName).HasMaxLength(120);
        builder.Property(a => a.Phone).HasMaxLength(30);
        builder.Property(a => a.Email).HasMaxLength(256);
        builder.Property(a => a.CurrentAddress).HasMaxLength(300);
        builder.Property(a => a.ReviewComment).HasMaxLength(2000);
        builder.Property(a => a.ManagerNotes).HasMaxLength(4000);
        builder.Ignore(a => a.IsEditable);
        builder.Ignore(a => a.IsTerminal);
        builder.Ignore(a => a.AllSectionsCompleted);

        // The domain only knows the applicant's id; the foreign key to the Identity user is an infrastructure concern.
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ApplicantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Residences).WithOne(r => r.RentalApplication).HasForeignKey(r => r.RentalApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.History).WithOne(h => h.RentalApplication).HasForeignKey(h => h.RentalApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Lease).WithOne(l => l.RentalApplication).HasForeignKey<Lease>(l => l.RentalApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}
