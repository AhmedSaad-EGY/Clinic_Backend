using Clinic.Domain.Packages;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Packages;

public sealed class PatientPackageConfiguration : IEntityTypeConfiguration<PatientPackage>
{
    public void Configure(EntityTypeBuilder<PatientPackage> builder)
    {
        builder.ToTable("PatientPackages", "packages", table =>
        {
            table.HasCheckConstraint("CK_PatientPackages_TotalSessions",
                "[TotalSessions] BETWEEN 1 AND 500");
            table.HasCheckConstraint("CK_PatientPackages_Prices",
                "[BasePriceSnapshot] >= 0 AND [NetPriceSnapshot] >= 0");
            table.HasCheckConstraint("CK_PatientPackages_Durations",
                $"[ActivationGraceDaysSnapshot] BETWEEN 1 AND {Package.MaximumDurationDays} AND " +
                $"[UsageDurationDaysSnapshot] BETWEEN 1 AND {Package.MaximumDurationDays}");
            table.HasCheckConstraint("CK_PatientPackages_PaymentStatus",
                "[PaymentStatus] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_PatientPackages_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_PatientPackages_PaymentTimeline",
                "([PaymentStatus] = 1 AND [NetPriceSnapshot] > 0 AND [ActivationWindowStartedAt] IS NULL AND [ActivationDeadlineAt] IS NULL) OR " +
                "([PaymentStatus] = 2 AND [NetPriceSnapshot] = 0 AND [ActivationWindowStartedAt] IS NOT NULL AND [ActivationDeadlineAt] IS NOT NULL) OR " +
                "([PaymentStatus] = 3 AND [NetPriceSnapshot] > 0 AND [ActivationWindowStartedAt] IS NOT NULL AND [ActivationDeadlineAt] IS NOT NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.DepartmentId });
        builder.HasAlternateKey(item => new { item.Id, item.PackageId });
        builder.HasAlternateKey(item => new { item.Id, item.PatientId });
        builder.Property(item => item.PackageNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.DepartmentNameSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(item => item.BasePriceSnapshot).HasPrecision(18, 2);
        builder.Property(item => item.NetPriceSnapshot).HasPrecision(18, 2);
        builder.Property(item => item.PaymentStatus).HasConversion<int>();
        builder.Property(item => item.Status).HasConversion<int>();
        builder.Property(item => item.RegisteredAt).HasPrecision(0);
        builder.Property(item => item.ActivationWindowStartedAt).HasPrecision(0);
        builder.Property(item => item.ActivationDeadlineAt).HasPrecision(0);
        builder.Property(item => item.FirstUsedAt).HasPrecision(0);
        builder.Property(item => item.ExpiresAt).HasPrecision(0);
        builder.Property(item => item.UpdatedAt).HasPrecision(0);
        builder.Property(item => item.RequestFingerprint).HasMaxLength(64).IsUnicode(false)
            .IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => item.IdempotencyKey).IsUnique()
            .HasDatabaseName("UX_PatientPackages_IdempotencyKey");
        builder.HasIndex(item => new { item.PatientId, item.RegisteredAt })
            .HasDatabaseName("IX_PatientPackages_Patient_RegisteredAt");
        builder.HasIndex(item => new { item.DepartmentId, item.Status, item.PaymentStatus })
            .HasDatabaseName("IX_PatientPackages_Department_Status");

        builder.HasOne(item => item.Patient).WithMany()
            .HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Package).WithMany()
            .HasForeignKey(item => new { item.PackageId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.UpdatedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Services).WithOne(item => item.PatientPackage)
            .HasForeignKey(item => new { item.PatientPackageId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
