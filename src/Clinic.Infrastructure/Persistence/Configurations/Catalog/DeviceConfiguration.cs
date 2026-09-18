namespace Clinic.Infrastructure.Persistence.Configurations.Catalog;

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices", "catalog");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.DepartmentId });
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Identifier).HasMaxLength(100);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => item.Identifier)
            .IsUnique()
            .HasFilter("[Identifier] IS NOT NULL AND [IsArchived] = 0")
            .HasDatabaseName("UX_Devices_Identifier_Active");

        builder.HasOne(item => item.Department)
            .WithMany()
            .HasForeignKey(item => item.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
