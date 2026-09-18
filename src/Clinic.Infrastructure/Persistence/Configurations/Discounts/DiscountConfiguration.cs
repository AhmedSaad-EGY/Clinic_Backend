namespace Clinic.Infrastructure.Persistence.Configurations.Discounts;

public sealed class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.ToTable("Discounts", "discounts", table =>
        {
            table.HasCheckConstraint("CK_Discounts_Type", "[Type] IN (1, 2)");
            table.HasCheckConstraint("CK_Discounts_AppliesTo", "[AppliesTo] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Discounts_ScopeMode", "[ScopeMode] IN (1, 2)");
            table.HasCheckConstraint("CK_Discounts_Value",
                "([Type] = 1 AND [Value] BETWEEN 0 AND 100) OR ([Type] = 2 AND [Value] > 0)");
            table.HasCheckConstraint("CK_Discounts_Period", "[StartAt] < [EndAt]");
            table.HasCheckConstraint("CK_Discounts_Archive", "[IsArchived] = 0 OR [IsActive] = 0");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Type).HasConversion<int>();
        builder.Property(item => item.Value).HasPrecision(18, 2);
        builder.Property(item => item.AppliesTo).HasConversion<int>();
        builder.Property(item => item.ScopeMode).HasConversion<int>();
        builder.Property(item => item.StartAt).HasPrecision(0);
        builder.Property(item => item.EndAt).HasPrecision(0);
        builder.Property(item => item.CreatedAt).HasPrecision(0);
        builder.Property(item => item.UpdatedAt).HasPrecision(0);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.IsActive, item.IsArchived, item.StartAt, item.EndAt })
            .HasDatabaseName("IX_Discounts_EffectivePeriod");
        builder.HasIndex(item => new { item.AppliesTo, item.ScopeMode })
            .HasDatabaseName("IX_Discounts_AppliesTo_ScopeMode");

        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CreatedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.UpdatedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Departments).WithOne(item => item.Discount)
            .HasForeignKey(item => item.DiscountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Services).WithOne(item => item.Discount)
            .HasForeignKey(item => item.DiscountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Packages).WithOne(item => item.Discount)
            .HasForeignKey(item => item.DiscountId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Departments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Packages).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
