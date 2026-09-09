using Clinic.Domain.Scheduling;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class DepartmentClosureConfiguration : IEntityTypeConfiguration<DepartmentClosure>
{
    public void Configure(EntityTypeBuilder<DepartmentClosure> builder)
    {
        builder.ToTable("DepartmentClosures", "scheduling", table =>
            table.HasCheckConstraint("CK_DepartmentClosures_TimeRange", "[StartAt] < [EndAt]"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Reason).HasMaxLength(500).IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.DepartmentId, item.StartAt, item.EndAt })
            .HasDatabaseName("IX_DepartmentClosures_Department_TimeRange");
        builder.HasOne(item => item.Department)
            .WithMany()
            .HasForeignKey(item => item.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
