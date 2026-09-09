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
            table.HasCheckConstraint("CK_Appointments_PaymentStatus", "[PaymentStatus] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_Appointments_Amounts",
                "[SubtotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [NetAmount] >= 0 AND [NetAmount] = [SubtotalAmount] - [DiscountAmount]");
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

        builder.HasOne(item => item.Patient).WithMany().HasForeignKey(item => item.PatientId)
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
