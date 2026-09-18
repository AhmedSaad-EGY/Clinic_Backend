using DiscountService = Clinic.Domain.Discounts.DiscountService;

namespace Clinic.Infrastructure.Persistence.Configurations.Discounts;

public sealed class DiscountServiceConfiguration : IEntityTypeConfiguration<DiscountService>
{
    public void Configure(EntityTypeBuilder<DiscountService> builder)
    {
        builder.ToTable("DiscountServices", "discounts");
        builder.HasKey(item => new { item.DiscountId, item.ServiceId });
        builder.HasOne(item => item.Service).WithMany()
            .HasForeignKey(item => item.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.ServiceId)
            .HasDatabaseName("IX_DiscountServices_ServiceId");
    }
}
