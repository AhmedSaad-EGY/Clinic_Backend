namespace Clinic.Infrastructure.Persistence.Configurations.Catalog;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments", "catalog");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => item.Name)
            .IsUnique()
            .HasDatabaseName("UX_Departments_Name");

        builder.HasOne(item => item.Room)
            .WithOne(item => item.Department)
            .HasForeignKey<Room>(item => item.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
