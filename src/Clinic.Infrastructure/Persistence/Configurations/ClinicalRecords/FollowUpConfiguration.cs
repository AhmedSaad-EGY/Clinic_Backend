using Clinic.Domain.Catalog;
using Clinic.Domain.ClinicalRecords;
using Clinic.Domain.Patients;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.ClinicalRecords;

public sealed class FollowUpConfiguration : IEntityTypeConfiguration<FollowUp>
{
    public void Configure(EntityTypeBuilder<FollowUp> builder)
    {
        builder.ToTable("FollowUps", "clinical", table =>
        {
            table.HasCheckConstraint("CK_FollowUps_Status", "[Status] BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_FollowUps_Result",
                "([Status] = 1 AND [ResultAppointmentId] IS NULL AND [ClosedAt] IS NULL) OR " +
                "([Status] = 2 AND [ResultAppointmentId] IS NOT NULL AND [ClosedAt] IS NOT NULL) OR " +
                "([Status] = 3 AND [ResultAppointmentId] IS NULL AND [ClosedAt] IS NOT NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.PrescriptionId).IsUnique();
        builder.HasIndex(item => new { item.Status, item.ReturnDate });
        builder.HasIndex(item => new { item.PatientId, item.ReturnDate });
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.CreatedAt).HasPrecision(0);
        builder.Property(item => item.ClosedAt).HasPrecision(0);
        builder.Property(item => item.ClosureReason).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasOne(item => item.Prescription).WithOne()
            .HasForeignKey<FollowUp>(item => item.PrescriptionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany()
            .HasForeignKey(item => item.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(item => item.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ResultAppointment).WithOne()
            .HasForeignKey<FollowUp>(item => new { item.ResultAppointmentId, item.PatientId })
            .HasPrincipalKey<Clinic.Domain.Appointments.Appointment>(
                item => new { item.Id, item.PatientId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
