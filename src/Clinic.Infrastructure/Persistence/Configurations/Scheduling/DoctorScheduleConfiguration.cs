namespace Clinic.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class DoctorScheduleConfiguration : IEntityTypeConfiguration<DoctorSchedule>
{
    public void Configure(EntityTypeBuilder<DoctorSchedule> builder)
    {
        builder.ToTable("DoctorSchedules", "scheduling", table =>
        {
            table.HasCheckConstraint("CK_DoctorSchedules_DayOfWeek", "[DayOfWeek] BETWEEN 1 AND 7");
            table.HasCheckConstraint("CK_DoctorSchedules_TimeRange", "[StartTime] < [EndTime]");
            table.HasCheckConstraint(
                "CK_DoctorSchedules_DateRange",
                "[EffectiveTo] IS NULL OR [EffectiveFrom] <= [EffectiveTo]");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DayOfWeek).HasConversion<int>().IsRequired();
        builder.Property(item => item.StartTime).HasColumnType("time(0)").IsRequired();
        builder.Property(item => item.EndTime).HasColumnType("time(0)").IsRequired();
        builder.Property(item => item.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(item => item.EffectiveTo).HasColumnType("date");
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.DoctorId, item.DayOfWeek, item.IsActive })
            .HasDatabaseName("IX_DoctorSchedules_Doctor_Day_Active");
        builder.HasOne(item => item.Doctor)
            .WithMany()
            .HasForeignKey(item => item.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
