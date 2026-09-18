namespace Clinic.Infrastructure.Persistence.Configurations.ClinicalRecords;

public sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescriptions", "clinical", table =>
            table.HasCheckConstraint("CK_Prescriptions_Status", "[Status] BETWEEN 1 AND 3"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).UseIdentityColumn();
        builder.HasAlternateKey(item => new { item.Id, item.AppointmentServiceId });
        builder.HasIndex(item => item.AppointmentServiceId).IsUnique();
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.CreatedAt).HasPrecision(0);
        builder.Property(item => item.FinalizedAt).HasPrecision(0);
        builder.Property(item => item.VoidedAt).HasPrecision(0);
        builder.Property(item => item.VoidReason).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasOne(item => item.AppointmentService).WithMany()
            .HasForeignKey(item => item.AppointmentServiceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Revisions).WithOne(item => item.Prescription)
            .HasForeignKey(item => item.PrescriptionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CurrentRevision).WithMany()
            .HasForeignKey(item => new { item.CurrentRevisionId, item.Id })
            .HasPrincipalKey(item => new { item.Id, item.PrescriptionId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.FinalizedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.VoidedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Revisions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
