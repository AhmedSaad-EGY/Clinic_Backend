using Clinic.Domain.Packages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Packages;

public sealed class PackageSessionBookingConfiguration
    : IEntityTypeConfiguration<PackageSessionBooking>
{
    public void Configure(EntityTypeBuilder<PackageSessionBooking> builder)
    {
        builder.ToTable("PackageSessionBookings", "packages", table =>
        {
            table.HasTrigger("TR_PackageSessionBookings_ValidateOwnership");
            table.HasCheckConstraint("CK_PackageSessionBookings_Status",
                "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_PackageSessionBookings_Timeline",
                "([Status] = 1 AND [ReleasedAt] IS NULL AND [ConsumedAt] IS NULL) OR " +
                "([Status] = 2 AND [ReleasedAt] IS NOT NULL AND [ConsumedAt] IS NULL) OR " +
                "([Status] = 3 AND [ReleasedAt] IS NULL AND [ConsumedAt] IS NOT NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).HasConversion<int>();
        builder.Property(item => item.ReservedAt).HasPrecision(0);
        builder.Property(item => item.ReleasedAt).HasPrecision(0);
        builder.Property(item => item.ConsumedAt).HasPrecision(0);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.PackageSessionId).IsUnique()
            .HasFilter("[Status] <> 2")
            .HasDatabaseName("UX_PackageSessionBookings_ReservedSession");

        builder.HasOne(item => item.Session).WithMany()
            .HasForeignKey(item => new
                { item.PackageSessionId, item.PatientPackageId, item.ServiceId })
            .HasPrincipalKey(item => new { item.Id, item.PatientPackageId, item.ServiceId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Appointment).WithMany()
            .HasForeignKey(item => new { item.AppointmentId, item.PatientId })
            .HasPrincipalKey(item => new { item.Id, item.PatientId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
