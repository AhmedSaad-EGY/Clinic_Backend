using Clinic.Domain.Approvals;
using Clinic.Domain.Cashier;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", "cashier", table =>
        {
            table.HasCheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Refunds_Status", "[Status] IN (1, 2)");
        });
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.OriginalPaymentId });
        builder.Property(item => item.TransactionNumber).HasMaxLength(40)
            .IsUnicode(false).IsRequired();
        builder.Property(item => item.RequestFingerprint).HasMaxLength(64)
            .IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.ExecutedAt).HasPrecision(0);
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.Note).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.TransactionNumber).IsUnique()
            .HasDatabaseName("UX_Refunds_TransactionNumber");
        builder.HasIndex(item => item.IdempotencyKey).IsUnique()
            .HasDatabaseName("UX_Refunds_IdempotencyKey");
        builder.HasIndex(item => item.ApprovalRequestId).IsUnique()
            .HasDatabaseName("UX_Refunds_ApprovalRequestId");
        builder.HasIndex(item => new { item.ExecutionShiftId, item.ExecutedAt })
            .HasDatabaseName("IX_Refunds_Shift_ExecutedAt");

        builder.HasOne<ApprovalRequest>().WithMany()
            .HasForeignKey(item => item.ApprovalRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany()
            .HasForeignKey(item => item.OriginalPaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Shift>().WithMany()
            .HasForeignKey(item => item.ExecutionShiftId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.ExecutedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.MethodAllocations).WithOne(item => item.Refund)
            .HasForeignKey(item => new { item.RefundId, item.OriginalPaymentId })
            .HasPrincipalKey(item => new { item.Id, item.OriginalPaymentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.AppointmentAllocations).WithOne(item => item.Refund)
            .HasForeignKey(item => new { item.RefundId, item.OriginalPaymentId })
            .HasPrincipalKey(item => new { item.Id, item.OriginalPaymentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.MethodAllocations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.AppointmentAllocations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
