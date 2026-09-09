using Clinic.Domain.Cashier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class RefundAppointmentAllocationConfiguration
    : IEntityTypeConfiguration<RefundAppointmentAllocation>
{
    public void Configure(EntityTypeBuilder<RefundAppointmentAllocation> builder)
    {
        builder.ToTable("RefundAppointmentAllocations", "cashier", table =>
            table.HasCheckConstraint("CK_RefundAppointmentAllocations_Amount", "[Amount] > 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.HasIndex(item => item.OriginalAllocationId).IsUnique()
            .HasDatabaseName("UX_RefundAppointmentAllocations_OriginalAllocationId");
        builder.HasOne(item => item.OriginalAllocation).WithMany()
            .HasForeignKey(item => new { item.OriginalAllocationId,
                item.OriginalPaymentId })
            .HasPrincipalKey(item => new { item.Id, item.PaymentId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
