using Clinic.Domain.Cashier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class AppointmentPaymentAllocationConfiguration
    : IEntityTypeConfiguration<AppointmentPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<AppointmentPaymentAllocation> builder)
    {
        builder.ToTable("AppointmentPaymentAllocations", "cashier", table =>
        {
            table.HasCheckConstraint("CK_AppointmentPaymentAllocations_Amount", "[Amount] > 0");
        });
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.PaymentId });
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.HasIndex(item => item.AppointmentId).IsUnique()
            .HasDatabaseName("UX_AppointmentPaymentAllocations_AppointmentId");
        builder.HasIndex(item => new { item.PaymentId, item.AppointmentId })
            .IsUnique().HasDatabaseName("UX_AppointmentPaymentAllocations_Payment_Appointment");
        builder.HasOne(item => item.Appointment).WithMany()
            .HasForeignKey(item => new { item.AppointmentId, item.PatientId })
            .HasPrincipalKey(item => new { item.Id, item.PatientId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
