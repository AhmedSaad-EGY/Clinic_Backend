using Clinic.Domain.Packages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Packages;

public sealed class PackageSessionConfiguration : IEntityTypeConfiguration<PackageSession>
{
    public void Configure(EntityTypeBuilder<PackageSession> builder)
    {
        builder.ToTable("PackageSessions", "packages", table =>
        {
            table.HasCheckConstraint("CK_PackageSessions_SequenceNumber",
                "[SequenceNumber] > 0");
            table.HasCheckConstraint("CK_PackageSessions_UnitPriceSnapshot",
                "[UnitPriceSnapshot] > 0");
            table.HasCheckConstraint("CK_PackageSessions_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_PackageSessions_StatusTimeline",
                "([Status] = 1 AND [ReservedAt] IS NULL AND [ConsumedAt] IS NULL) OR " +
                "([Status] = 2 AND [ReservedAt] IS NOT NULL AND [ConsumedAt] IS NULL) OR " +
                "([Status] = 3 AND [ConsumedAt] IS NOT NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.UnitPriceSnapshot).HasPrecision(18, 2);
        builder.Property(item => item.Status).HasConversion<int>();
        builder.Property(item => item.ReservedAt).HasPrecision(0);
        builder.Property(item => item.ConsumedAt).HasPrecision(0);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.PatientPackageServiceId, item.SequenceNumber })
            .IsUnique().HasDatabaseName("UX_PackageSessions_Service_Sequence");
        builder.HasIndex(item => new { item.PatientPackageId, item.Status })
            .HasDatabaseName("IX_PackageSessions_PatientPackage_Status");
    }
}
