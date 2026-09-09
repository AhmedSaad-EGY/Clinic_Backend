using Clinic.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Catalog;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services", "catalog", table =>
        {
            table.HasCheckConstraint(
                "CK_Services_DurationMinutes",
                "[DurationMinutes] > 0 AND [DurationMinutes] <= 1440");
            table.HasCheckConstraint(
                "CK_Services_CurrentUnitPrice",
                "[CurrentUnitPrice] > 0");
            table.HasCheckConstraint(
                "CK_Services_ServiceType",
                "[ServiceType] IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "CK_Services_PricingMode",
                "[PricingMode] IN (1, 2)");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.DepartmentId });
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ServiceType).HasConversion<int>().IsRequired();
        builder.Property(item => item.PricingMode).HasConversion<int>().IsRequired();
        builder.Property(item => item.CurrentUnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.SpecializationId, item.Name })
            .IsUnique()
            .HasDatabaseName("UX_Services_SpecializationId_Name");

        builder.HasOne(item => item.Department)
            .WithMany()
            .HasForeignKey(item => item.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Specialization)
            .WithMany()
            .HasForeignKey(item => new { item.SpecializationId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.PriceHistory)
            .WithOne(item => item.Service)
            .HasForeignKey(item => item.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.DeviceAssignments)
            .WithOne(item => item.Service)
            .HasForeignKey(item => new { item.ServiceId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(item => item.PriceHistory)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.DeviceAssignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
