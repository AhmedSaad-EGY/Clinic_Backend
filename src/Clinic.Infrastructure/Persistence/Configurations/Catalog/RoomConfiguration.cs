using Clinic.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Catalog;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms", "catalog");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.DepartmentId });
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => item.DepartmentId)
            .IsUnique()
            .HasDatabaseName("UX_Rooms_DepartmentId");
    }
}
