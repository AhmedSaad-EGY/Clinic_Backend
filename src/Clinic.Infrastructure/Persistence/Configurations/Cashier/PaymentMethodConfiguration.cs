using Clinic.Domain.Cashier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PaymentMethods", "cashier", table =>
        {
            table.HasCheckConstraint("CK_PaymentMethods_SortOrder", "[SortOrder] > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(item => item.DisplayName).HasMaxLength(100).IsRequired();
        builder.HasIndex(item => item.Code).IsUnique()
            .HasDatabaseName("UX_PaymentMethods_Code");
        builder.HasIndex(item => item.SortOrder)
            .HasDatabaseName("IX_PaymentMethods_SortOrder");

        builder.HasData(
            new { Id = 1L, Code = "CASH", DisplayName = "نقدي", IsCash = true,
                IsActive = true, SortOrder = 1 },
            new { Id = 2L, Code = "VISA", DisplayName = "Visa", IsCash = false,
                IsActive = true, SortOrder = 2 },
            new { Id = 3L, Code = "INSTAPAY", DisplayName = "InstaPay", IsCash = false,
                IsActive = true, SortOrder = 3 },
            new { Id = 4L, Code = "WALLET", DisplayName = "محفظة", IsCash = false,
                IsActive = true, SortOrder = 4 });
    }
}
