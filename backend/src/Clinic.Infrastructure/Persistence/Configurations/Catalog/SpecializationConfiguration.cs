using Clinic.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Persistence.Configurations.Catalog;

public sealed class SpecializationConfiguration : IEntityTypeConfiguration<Specialization>
{
    public void Configure(EntityTypeBuilder<Specialization> builder)
    {
        builder.ToTable("Specializations", "catalog");
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.DepartmentId });
        builder.Property(item => item.Name).HasMaxLength(150).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.DepartmentId, item.Name })
            .IsUnique()
            .HasDatabaseName("UX_Specializations_DepartmentId_Name");

        builder.HasOne(item => item.Department)
            .WithMany()
            .HasForeignKey(item => item.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
