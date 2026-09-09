using Clinic.Domain.Catalog;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Catalog;

public sealed class ServicePriceHistoryConfiguration
    : IEntityTypeConfiguration<ServicePriceHistory>
{
    public void Configure(EntityTypeBuilder<ServicePriceHistory> builder)
    {
        builder.ToTable("ServicePriceHistory", "catalog", table =>
        {
            table.HasCheckConstraint(
                "CK_ServicePriceHistory_UnitPrice",
                "[UnitPrice] > 0");
            table.HasCheckConstraint(
                "CK_ServicePriceHistory_Period",
                "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);

        builder.HasIndex(item => item.ServiceId)
            .IsUnique()
            .HasFilter("[EffectiveTo] IS NULL")
            .HasDatabaseName("UX_ServicePriceHistory_CurrentPrice");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
