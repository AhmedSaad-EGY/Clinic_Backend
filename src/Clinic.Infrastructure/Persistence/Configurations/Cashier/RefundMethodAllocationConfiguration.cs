using Clinic.Domain.Cashier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class RefundMethodAllocationConfiguration
    : IEntityTypeConfiguration<RefundMethodAllocation>
{
    public void Configure(EntityTypeBuilder<RefundMethodAllocation> builder)
    {
        builder.ToTable("RefundMethodAllocations", "cashier", table =>
            table.HasCheckConstraint("CK_RefundMethodAllocations_Amount", "[Amount] > 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.ReferenceNumber).HasMaxLength(100);
        builder.HasIndex(item => new { item.RefundId, item.OriginalAllocationId })
            .IsUnique().HasDatabaseName("UX_RefundMethodAllocations_Refund_Original");
        builder.HasOne(item => item.OriginalAllocation).WithMany()
            .HasForeignKey(item => new { item.OriginalAllocationId,
                item.OriginalPaymentId })
            .HasPrincipalKey(item => new { item.Id, item.PaymentId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
