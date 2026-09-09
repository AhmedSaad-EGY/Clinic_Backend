using Clinic.Domain.Patients;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Patients;

public sealed class PatientNoteConfiguration : IEntityTypeConfiguration<PatientNote>
{
    public void Configure(EntityTypeBuilder<PatientNote> builder)
    {
        builder.ToTable("PatientNotes", "patients", table =>
            table.HasCheckConstraint("CK_PatientNotes_Visibility", "[Visibility] IN (1, 2)"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NoteText).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.Visibility).HasConversion<int>().IsRequired();
        builder.Property(item => item.CreatedAt).HasPrecision(0).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.PatientId, item.Visibility, item.IsArchived })
            .HasDatabaseName("IX_PatientNotes_Patient_Visibility_Archived");
        builder.HasOne(item => item.Patient).WithMany()
            .HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
