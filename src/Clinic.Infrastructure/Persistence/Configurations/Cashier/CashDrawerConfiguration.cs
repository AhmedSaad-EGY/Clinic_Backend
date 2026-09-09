using Clinic.Domain.Cashier;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class CashDrawerConfiguration : IEntityTypeConfiguration<CashDrawer>
{
    public void Configure(EntityTypeBuilder<CashDrawer> builder)
    {
        builder.ToTable("CashDrawers", "cashier");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(220).IsRequired();
        builder.Property(item => item.CreatedAt).HasPrecision(0);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.SecretaryUserId)
            .IsUnique()
            .HasDatabaseName("UX_CashDrawers_SecretaryUserId");
        builder.HasOne<ApplicationUser>().WithOne()
            .HasForeignKey<CashDrawer>(item => item.SecretaryUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
