namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class PackagePaymentAllocationConfiguration
    : IEntityTypeConfiguration<PackagePaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PackagePaymentAllocation> builder)
    {
        builder.ToTable("PackagePaymentAllocations", "cashier", table =>
            table.HasCheckConstraint("CK_PackagePaymentAllocations_Amount", "[Amount] > 0"));
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.PaymentId });
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.HasIndex(item => item.PatientPackageId).IsUnique()
            .HasDatabaseName("UX_PackagePaymentAllocations_PatientPackageId");
        builder.HasIndex(item => new { item.PaymentId, item.PatientPackageId }).IsUnique()
            .HasDatabaseName("UX_PackagePaymentAllocations_Payment_PatientPackage");
        builder.HasOne(item => item.PatientPackage).WithMany()
            .HasForeignKey(item => new { item.PatientPackageId, item.PatientId })
            .HasPrincipalKey(item => new { item.Id, item.PatientId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
