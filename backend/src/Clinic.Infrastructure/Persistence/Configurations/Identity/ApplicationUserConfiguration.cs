using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Identity;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users", "identity");

        builder.Property(user => user.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.NormalizedPhoneNumber)
            .HasMaxLength(32);

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(32);

        builder.Property(user => user.CreatedAt).HasPrecision(0);
        builder.Property(user => user.UpdatedAt).HasPrecision(0);
        builder.Property(user => user.LastLoginAt).HasPrecision(0);
        builder.Property(user => user.RowVersion).IsRowVersion();

        builder.HasIndex(user => user.NormalizedPhoneNumber)
            .IsUnique()
            .HasFilter("[NormalizedPhoneNumber] IS NOT NULL");

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("UX_Users_NormalizedEmail")
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(user => user.CreatedByAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
