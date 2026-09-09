using Clinic.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Catalog;

public sealed class ServiceDeviceConfiguration : IEntityTypeConfiguration<ServiceDevice>
{
    public void Configure(EntityTypeBuilder<ServiceDevice> builder)
    {
        builder.ToTable("ServiceDevices", "catalog");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.ServiceId, item.DepartmentId });
        builder.HasAlternateKey(item => new
        {
            item.Id,
            item.ServiceId,
            item.DepartmentId,
            item.DeviceId
        });

        builder.HasIndex(item => new { item.ServiceId, item.DeviceId })
            .IsUnique()
            .HasDatabaseName("UX_ServiceDevices_ServiceId_DeviceId");

        builder.HasOne(item => item.Device)
            .WithMany()
            .HasForeignKey(item => new { item.DeviceId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
