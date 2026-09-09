using Clinic.Domain.Packages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Packages;

public sealed class PackageServiceConfiguration : IEntityTypeConfiguration<PackageService>
{
    public void Configure(EntityTypeBuilder<PackageService> builder)
    {
        builder.ToTable("PackageServices", "packages", table =>
        {
            table.HasCheckConstraint("CK_PackageServices_SessionsIncluded",
                "[SessionsIncluded] > 0");
            table.HasCheckConstraint("CK_PackageServices_UnitPriceAtDefinition",
                "[UnitPriceAtDefinition] > 0");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.PackageId, item.ServiceId });
        builder.Property(item => item.UnitPriceAtDefinition).HasPrecision(18, 2);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.PackageId, item.ServiceId }).IsUnique()
            .HasDatabaseName("UX_PackageServices_PackageId_ServiceId");

        builder.HasOne(item => item.Service).WithMany()
            .HasForeignKey(item => new { item.ServiceId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
