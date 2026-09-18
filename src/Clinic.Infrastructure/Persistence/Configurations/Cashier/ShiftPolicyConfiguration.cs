namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class ShiftPolicyConfiguration : IEntityTypeConfiguration<ShiftPolicy>
{
    public void Configure(EntityTypeBuilder<ShiftPolicy> builder)
    {
        builder.ToTable("ShiftPolicies", "cashier", table =>
        {
            table.HasCheckConstraint("CK_ShiftPolicies_Singleton", "[Id] = 1");
            table.HasCheckConstraint("CK_ShiftPolicies_ClosingGraceMinutes",
                "[ClosingGraceMinutes] BETWEEN 0 AND 120");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.UpdatedAt).HasPrecision(0);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.UpdatedByAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new
        {
            Id = ShiftPolicy.SingletonId,
            ClosingGraceMinutes = ShiftPolicy.DefaultClosingGraceMinutes
        });
    }
}
