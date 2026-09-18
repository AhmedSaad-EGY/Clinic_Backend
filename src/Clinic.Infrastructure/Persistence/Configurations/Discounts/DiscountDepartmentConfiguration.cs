namespace Clinic.Infrastructure.Persistence.Configurations.Discounts;

public sealed class DiscountDepartmentConfiguration
    : IEntityTypeConfiguration<DiscountDepartment>
{
    public void Configure(EntityTypeBuilder<DiscountDepartment> builder)
    {
        builder.ToTable("DiscountDepartments", "discounts");
        builder.HasKey(item => new { item.DiscountId, item.DepartmentId });
        builder.HasOne(item => item.Department).WithMany()
            .HasForeignKey(item => item.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.DepartmentId)
            .HasDatabaseName("IX_DiscountDepartments_DepartmentId");
    }
}
