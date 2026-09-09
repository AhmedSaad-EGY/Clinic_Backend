using Clinic.Domain.Patients;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Patients;

public sealed class TreatmentHistoryConfiguration : IEntityTypeConfiguration<TreatmentHistory>
{
    public void Configure(EntityTypeBuilder<TreatmentHistory> builder)
    {
        builder.ToTable("TreatmentHistory", "patients");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EventDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.Description).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.CreatedAt).HasPrecision(0).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.PatientId, item.EventDate, item.IsArchived })
            .HasDatabaseName("IX_TreatmentHistory_Patient_Date_Archived");
        builder.HasOne(item => item.Patient).WithMany()
            .HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
