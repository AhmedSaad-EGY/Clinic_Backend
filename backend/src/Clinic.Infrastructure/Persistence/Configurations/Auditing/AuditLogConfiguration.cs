using Clinic.Domain.Auditing;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Auditing;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable(
            "AuditLogs",
            "audit",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_AuditLogs_Actor",
                "([ActorType] = 1 AND [ActorUserId] IS NOT NULL) OR " +
                "([ActorType] = 2 AND [ActorUserId] IS NULL)"));

        builder.HasKey(auditLog => auditLog.Id);

        builder.Property(auditLog => auditLog.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditLog => auditLog.OccurredAt).HasPrecision(0);
        builder.Property(auditLog => auditLog.Reason).HasMaxLength(500);
        builder.Property(auditLog => auditLog.DataJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(auditLog => auditLog.OccurredAt);
        builder.HasIndex(auditLog => new { auditLog.EntityType, auditLog.EntityId });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(auditLog => auditLog.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
