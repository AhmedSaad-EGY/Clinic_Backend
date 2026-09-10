using Clinic.Domain.Discounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Discounts;

public sealed class DiscountPackageConfiguration : IEntityTypeConfiguration<DiscountPackage>
{
    public void Configure(EntityTypeBuilder<DiscountPackage> builder)
    {
        builder.ToTable("DiscountPackages", "discounts");
        builder.HasKey(item => new { item.DiscountId, item.PackageId });
        builder.HasOne(item => item.Package).WithMany()
            .HasForeignKey(item => item.PackageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.PackageId)
            .HasDatabaseName("IX_DiscountPackages_PackageId");
    }
}
