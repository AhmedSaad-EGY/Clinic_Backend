namespace Clinic.Infrastructure.Persistence.Configurations.ClinicalRecords;

public sealed class PrescriptionRevisionConfiguration
    : IEntityTypeConfiguration<PrescriptionRevision>
{
    public void Configure(EntityTypeBuilder<PrescriptionRevision> builder)
    {
        builder.ToTable("PrescriptionRevisions", "clinical", table =>
            table.HasCheckConstraint("CK_PrescriptionRevisions_Number",
                "[RevisionNumber] > 0"));
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.PrescriptionId });
        builder.HasIndex(item => new { item.PrescriptionId, item.RevisionNumber })
            .IsUnique();
        builder.Property(item => item.CreatedAt).HasPrecision(0);
        builder.Property(item => item.ChangeReason).HasMaxLength(500);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Items).WithOne(item => item.PrescriptionRevision)
            .HasForeignKey(item => item.PrescriptionRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
