using Clinic.Domain.Appointments;
using Clinic.Domain.Catalog;
using Clinic.Domain.Patients;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Appointments;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments", "appointments", table =>
        {
            table.HasCheckConstraint("CK_Appointments_TimeRange", "[StartAt] < [EndAt]");
            table.HasCheckConstraint("CK_Appointments_Status", "[Status] IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint("CK_Appointments_StatusBeforeSuspension",
                "([Status] = 6 AND [StatusBeforeSuspension] IN (1, 2)) OR " +
                "([Status] <> 6 AND [StatusBeforeSuspension] IS NULL)");
            table.HasCheckConstraint("CK_Appointments_PaymentStatus", "[PaymentStatus] IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint("CK_Appointments_Amounts",
                "[SubtotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [PackageCoveredAmount] >= 0 AND [NetAmount] >= 0 AND [NetAmount] = [SubtotalAmount] - [DiscountAmount] - [PackageCoveredAmount]");
            table.HasCheckConstraint("CK_Appointments_PackageLink",
                "([PatientPackageId] IS NULL AND [PackageCoveredAmount] = 0 AND [IdempotencyKey] IS NULL AND [RequestFingerprint] IS NULL) OR " +
                "([PatientPackageId] IS NOT NULL AND [PackageCoveredAmount] > 0 AND [PaymentStatus] = 5 AND [IdempotencyKey] IS NOT NULL AND LEN([RequestFingerprint]) = 64)");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.DepartmentId });
        builder.HasAlternateKey(item => new { item.Id, item.PatientId });
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.StatusBeforeSuspension).HasConversion<int?>();
        builder.Property(item => item.PaymentStatus).HasConversion<int>().IsRequired();
        builder.Property(item => item.SubtotalAmount).HasPrecision(18, 2);
        builder.Property(item => item.DiscountAmount).HasPrecision(18, 2);
        builder.Property(item => item.NetAmount).HasPrecision(18, 2);
        builder.Property(item => item.PackageCoveredAmount).HasPrecision(18, 2);
        builder.Property(item => item.RequestFingerprint).HasMaxLength(64).IsUnicode(false);
        builder.Property(item => item.StartAt).HasPrecision(0);
        builder.Property(item => item.EndAt).HasPrecision(0);
        builder.Property(item => item.CreatedAt).HasPrecision(0);
        builder.Property(item => item.UpdatedAt).HasPrecision(0);
        builder.Property(item => item.CancelledAt).HasPrecision(0);
        builder.Property(item => item.CancellationReason).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.DepartmentId, item.StartAt, item.EndAt })
            .HasDatabaseName("IX_Appointments_Department_TimeRange");
        builder.HasIndex(item => new { item.PatientId, item.StartAt })
            .HasDatabaseName("IX_Appointments_Patient_StartAt");
        builder.HasIndex(item => new { item.Status, item.StartAt })
            .HasDatabaseName("IX_Appointments_Status_StartAt");
        builder.HasIndex(item => item.IdempotencyKey).IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_Appointments_Package_IdempotencyKey");

        builder.HasOne(item => item.Patient).WithMany().HasForeignKey(item => item.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PatientPackage).WithMany()
            .HasForeignKey(item => new { item.PatientPackageId, item.PatientId })
            .HasPrincipalKey(item => new { item.Id, item.PatientId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Room).WithMany()
            .HasForeignKey(item => new { item.RoomId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Services).WithOne(item => item.Appointment)
            .HasForeignKey(item => new { item.AppointmentId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
