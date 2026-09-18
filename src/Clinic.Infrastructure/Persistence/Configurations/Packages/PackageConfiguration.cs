namespace Clinic.Infrastructure.Persistence.Configurations.Packages;

public sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Packages", "packages", table =>
        {
            table.HasCheckConstraint("CK_Packages_SessionCount",
                "[SessionCount] > 0 AND [SessionCount] <= 500");
            table.HasCheckConstraint("CK_Packages_BasePrice", "[BasePrice] >= 0");
            table.HasCheckConstraint("CK_Packages_ActivationGraceDays",
                $"[ActivationGraceDays] IS NULL OR [ActivationGraceDays] BETWEEN 1 AND {Package.MaximumDurationDays}");
            table.HasCheckConstraint("CK_Packages_UsageDurationDays",
                $"[UsageDurationDays] IS NULL OR [UsageDurationDays] BETWEEN 1 AND {Package.MaximumDurationDays}");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.DepartmentId });
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.BasePrice).HasPrecision(18, 2);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => item.Name).IsUnique()
            .HasDatabaseName("UX_Packages_Name");
        builder.HasIndex(item => new { item.DepartmentId, item.IsArchived, item.IsActive })
            .HasDatabaseName("IX_Packages_Department_Status");

        builder.HasOne(item => item.Department).WithMany()
            .HasForeignKey(item => item.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CreatedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.UpdatedByAdminUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.Services).WithOne(item => item.Package)
            .HasForeignKey(item => new { item.PackageId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
