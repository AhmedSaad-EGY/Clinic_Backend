using Clinic.Domain.Cashier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class PaymentMethodAllocationConfiguration
    : IEntityTypeConfiguration<PaymentMethodAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentMethodAllocation> builder)
    {
        builder.ToTable("PaymentMethodAllocations", "cashier", table =>
        {
            table.HasCheckConstraint("CK_PaymentMethodAllocations_Amount", "[Amount] > 0");
        });
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.PaymentId });
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.ReferenceNumber).HasMaxLength(100);
        builder.HasIndex(item => new { item.PaymentId, item.PaymentMethodId })
            .IsUnique().HasDatabaseName("UX_PaymentMethodAllocations_Payment_Method");
        builder.HasOne(item => item.PaymentMethod).WithMany()
            .HasForeignKey(item => item.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }
}
