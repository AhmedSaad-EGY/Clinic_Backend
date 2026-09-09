using Clinic.Domain.Packages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Packages;

public sealed class PatientPackageServiceConfiguration
    : IEntityTypeConfiguration<PatientPackageService>
{
    public void Configure(EntityTypeBuilder<PatientPackageService> builder)
    {
        builder.ToTable("PatientPackageServices", "packages", table =>
        {
            table.HasCheckConstraint("CK_PatientPackageServices_SessionsPurchased",
                "[SessionsPurchased] > 0");
            table.HasCheckConstraint("CK_PatientPackageServices_UnitPriceSnapshot",
                "[UnitPriceSnapshot] > 0");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.PatientPackageId, item.ServiceId });
        builder.Property(item => item.ServiceNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SpecializationNameSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(item => item.UnitPriceSnapshot).HasPrecision(18, 2);
        builder.HasIndex(item => new { item.PatientPackageId, item.ServiceId }).IsUnique()
            .HasDatabaseName("UX_PatientPackageServices_Package_Service");

        builder.HasOne(item => item.SourcePackageService).WithMany()
            .HasForeignKey(item => new
            {
                item.SourcePackageServiceId,
                item.SourcePackageId,
                item.ServiceId
            })
            .HasPrincipalKey(item => new { item.Id, item.PackageId, item.ServiceId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PatientPackage>().WithMany()
            .HasForeignKey(item => new { item.PatientPackageId, item.SourcePackageId })
            .HasPrincipalKey(item => new { item.Id, item.PackageId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Catalog.Service>().WithMany()
            .HasForeignKey(item => new { item.ServiceId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Sessions).WithOne(item => item.PatientPackageService)
            .HasForeignKey(item => new
            {
                item.PatientPackageServiceId,
                item.PatientPackageId,
                item.ServiceId
            })
            .HasPrincipalKey(item => new { item.Id, item.PatientPackageId, item.ServiceId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Sessions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
