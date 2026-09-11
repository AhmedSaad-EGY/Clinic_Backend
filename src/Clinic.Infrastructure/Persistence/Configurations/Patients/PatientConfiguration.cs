using Clinic.Domain.Patients;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Patients;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients", "patients", table =>
        {
            table.HasCheckConstraint("CK_Patients_AgeSource",
                "([BirthDate] IS NOT NULL AND [AgeAtRegistration] IS NULL AND [AgeRecordedAt] IS NULL) OR " +
                "([BirthDate] IS NULL AND [AgeAtRegistration] IS NOT NULL AND [AgeRecordedAt] IS NOT NULL)");
            table.HasCheckConstraint("CK_Patients_AgeRange",
                "[AgeAtRegistration] IS NULL OR [AgeAtRegistration] BETWEEN 0 AND 130");
            table.HasCheckConstraint("CK_Patients_Gender", "[Gender] IN (1, 2)");
            table.HasCheckConstraint("CK_Patients_DifferentPhones",
                "[SecondaryPhoneNumber] IS NULL OR [SecondaryPhoneNumber] <> [PrimaryPhoneNumber]");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.FileNumber)
            .HasDefaultValueSql("NEXT VALUE FOR [patients].[PatientFileNumberSequence]")
            .ValueGeneratedOnAdd();
        builder.Property(item => item.FullName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.PrimaryPhoneNumber).HasMaxLength(11).IsRequired();
        builder.Property(item => item.SecondaryPhoneNumber).HasMaxLength(11);
        builder.Property(item => item.BirthDate).HasColumnType("date");
        builder.Property(item => item.AgeRecordedAt).HasColumnType("date");
        builder.Property(item => item.Gender).HasConversion<int>().IsRequired();
        builder.Property(item => item.Area).HasMaxLength(150);
        builder.Property(item => item.Address).HasMaxLength(500);
        builder.Property(item => item.Email).HasMaxLength(256);
        builder.Property(item => item.GuardianName).HasMaxLength(200);
        builder.Property(item => item.GuardianPhoneNumber).HasMaxLength(11);
        builder.Property(item => item.CreatedAt).HasPrecision(0).IsRequired();
        builder.Property(item => item.UpdatedAt).HasPrecision(0);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => item.FileNumber).IsUnique()
            .HasDatabaseName("UX_Patients_FileNumber");
        builder.HasIndex(item => item.PrimaryPhoneNumber).IsUnique()
            .HasDatabaseName("UX_Patients_PrimaryPhoneNumber");
        builder.HasIndex(item => item.SecondaryPhoneNumber).IsUnique()
            .HasFilter("[SecondaryPhoneNumber] IS NOT NULL")
            .HasDatabaseName("UX_Patients_SecondaryPhoneNumber");
        builder.HasIndex(item => item.FullName).HasDatabaseName("IX_Patients_FullName");
        builder.HasIndex(item => new { item.Area, item.Gender })
            .HasDatabaseName("IX_Patients_Area_Gender");

        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
