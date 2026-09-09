using Clinic.Domain.Appointments;
using Clinic.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Appointments;

public sealed class AppointmentDeviceConfiguration : IEntityTypeConfiguration<AppointmentDevice>
{
    public void Configure(EntityTypeBuilder<AppointmentDevice> builder)
    {
        builder.ToTable("AppointmentDevices", "appointments", table =>
            table.HasCheckConstraint("CK_AppointmentDevices_TimeRange", "[ReservedFrom] < [ReservedTo]"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ReservedFrom).HasPrecision(0);
        builder.Property(item => item.ReservedTo).HasPrecision(0);
        builder.HasIndex(item => new { item.AppointmentServiceId, item.ServiceDeviceId })
            .IsUnique().HasDatabaseName("UX_AppointmentDevices_Service_Device");
        builder.HasIndex(item => new { item.DeviceId, item.ReservedFrom, item.ReservedTo })
            .HasDatabaseName("IX_AppointmentDevices_Device_TimeRange");
        builder.HasOne(item => item.ServiceDevice).WithMany()
            .HasForeignKey(item => new
            {
                item.ServiceDeviceId,
                item.ServiceId,
                item.DepartmentId,
                item.DeviceId
            })
            .HasPrincipalKey(item => new
            {
                item.Id,
                item.ServiceId,
                item.DepartmentId,
                item.DeviceId
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Device).WithMany()
            .HasForeignKey(item => new { item.DeviceId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
