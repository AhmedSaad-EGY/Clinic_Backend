namespace Clinic.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors", "scheduling");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.DepartmentId });
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.Phone).HasMaxLength(30);
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.DepartmentId, item.Name })
            .HasDatabaseName("IX_Doctors_DepartmentId_Name");
        builder.HasOne(item => item.Department)
            .WithMany()
            .HasForeignKey(item => item.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
