namespace Clinic.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class DoctorExceptionConfiguration : IEntityTypeConfiguration<DoctorScheduleOverride>
{
    public void Configure(EntityTypeBuilder<DoctorScheduleOverride> builder)
    {
        builder.ToTable("DoctorExceptions", "scheduling", table =>
        {
            table.HasCheckConstraint("CK_DoctorExceptions_Type", "[Type] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_DoctorExceptions_TimeRange",
                "([StartTime] IS NULL AND [EndTime] IS NULL) OR " +
                "([StartTime] IS NOT NULL AND [EndTime] IS NOT NULL AND [StartTime] < [EndTime])");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ExceptionDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.StartTime).HasColumnType("time(0)");
        builder.Property(item => item.EndTime).HasColumnType("time(0)");
        builder.Property(item => item.Type).HasConversion<int>().IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.DoctorId, item.ExceptionDate, item.CancelledAt })
            .HasDatabaseName("IX_DoctorExceptions_Doctor_Date_CancelledAt");
        builder.HasOne(item => item.Doctor)
            .WithMany()
            .HasForeignKey(item => item.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
