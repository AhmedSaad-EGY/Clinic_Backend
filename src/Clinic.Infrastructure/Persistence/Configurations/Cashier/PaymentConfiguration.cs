using Clinic.Domain.Cashier;
using Clinic.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", "cashier", table =>
        {
            table.HasCheckConstraint("CK_Payments_TotalAmount", "[TotalAmount] > 0");
            table.HasCheckConstraint("CK_Payments_Status", "[Status] BETWEEN 1 AND 3");
        });
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.PatientId });
        builder.Property(item => item.TransactionNumber).HasMaxLength(40)
            .IsUnicode(false).IsRequired();
        builder.Property(item => item.RequestFingerprint).HasMaxLength(64)
            .IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(item => item.TotalAmount).HasPrecision(18, 2);
        builder.Property(item => item.CollectedAt).HasPrecision(0);
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.Note).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.TransactionNumber).IsUnique()
            .HasDatabaseName("UX_Payments_TransactionNumber");
        builder.HasIndex(item => item.IdempotencyKey).IsUnique()
            .HasDatabaseName("UX_Payments_IdempotencyKey");
        builder.HasIndex(item => new { item.ShiftId, item.CollectedAt })
            .HasDatabaseName("IX_Payments_Shift_CollectedAt");
        builder.HasIndex(item => new { item.PatientId, item.CollectedAt })
            .HasDatabaseName("IX_Payments_Patient_CollectedAt");

        builder.HasOne(item => item.Shift).WithMany()
            .HasForeignKey(item => item.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Patient).WithMany()
            .HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CollectedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.MethodAllocations).WithOne(item => item.Payment)
            .HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.AppointmentAllocations).WithOne(item => item.Payment)
            .HasForeignKey(item => new { item.PaymentId, item.PatientId })
            .HasPrincipalKey(item => new { item.Id, item.PatientId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.MethodAllocations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.AppointmentAllocations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
