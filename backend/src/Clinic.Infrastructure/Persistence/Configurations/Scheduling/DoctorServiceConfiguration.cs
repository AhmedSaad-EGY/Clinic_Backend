using Clinic.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class DoctorServiceConfiguration : IEntityTypeConfiguration<DoctorService>
{
    public void Configure(EntityTypeBuilder<DoctorService> builder)
    {
        builder.ToTable("DoctorServices", "scheduling");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.ServiceId, item.DepartmentId });
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.DoctorId, item.ServiceId })
            .IsUnique()
            .HasDatabaseName("UX_DoctorServices_DoctorId_ServiceId");
        builder.HasOne(item => item.Doctor)
            .WithMany()
            .HasForeignKey(item => new { item.DoctorId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Service)
            .WithMany()
            .HasForeignKey(item => new { item.ServiceId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
