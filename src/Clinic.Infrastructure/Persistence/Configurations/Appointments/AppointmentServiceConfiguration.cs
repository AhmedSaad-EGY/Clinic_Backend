using AppointmentService = Clinic.Domain.Appointments.AppointmentService;

namespace Clinic.Infrastructure.Persistence.Configurations.Appointments;

public sealed class AppointmentServiceConfiguration : IEntityTypeConfiguration<AppointmentService>
{
    public void Configure(EntityTypeBuilder<AppointmentService> builder)
    {
        builder.ToTable("AppointmentServices", "appointments", table =>
        {
            table.HasCheckConstraint("CK_AppointmentServices_TimeRange", "[SegmentStartAt] < [SegmentEndAt]");
            table.HasCheckConstraint("CK_AppointmentServices_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_AppointmentServices_Amounts",
                "[UnitPrice] > 0 AND [GrossAmount] = [UnitPrice] * [Quantity] AND [DiscountAmount] >= 0 AND [PackageCoveredAmount] >= 0 AND [NetAmount] >= 0 AND [NetAmount] = [GrossAmount] - [DiscountAmount] - [PackageCoveredAmount]");
            table.HasCheckConstraint("CK_AppointmentServices_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_AppointmentServices_Discount",
                "([DiscountId] IS NOT NULL OR [DiscountAmount] = 0) AND " +
                "([DiscountOverrideMode] IS NULL AND [DiscountOverrideByAdminUserId] IS NULL AND [DiscountOverrideReason] IS NULL OR " +
                "[DiscountOverrideMode] = 1 AND [DiscountId] IS NOT NULL AND [DiscountOverrideByAdminUserId] IS NOT NULL AND ([DiscountOverrideReason] IS NULL OR LEN([DiscountOverrideReason]) BETWEEN 1 AND 500) OR " +
                "[DiscountOverrideMode] = 2 AND [DiscountId] IS NULL AND [DiscountAmount] = 0 AND [DiscountOverrideByAdminUserId] IS NOT NULL AND ([DiscountOverrideReason] IS NULL OR LEN([DiscountOverrideReason]) BETWEEN 1 AND 500))");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.ServiceId, item.DepartmentId });
        builder.HasAlternateKey(item => new { item.Id, item.ServiceId });
        builder.HasAlternateKey(item => new { item.Id, item.AppointmentId, item.ServiceId });
        builder.HasAlternateKey(item => new { item.AppointmentId, item.SequenceNumber });
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.GrossAmount).HasPrecision(18, 2);
        builder.Property(item => item.DiscountAmount).HasPrecision(18, 2);
        builder.Property(item => item.DiscountOverrideMode).HasConversion<int?>();
        builder.Property(item => item.DiscountOverrideReason).HasMaxLength(500);
        builder.Property(item => item.NetAmount).HasPrecision(18, 2);
        builder.Property(item => item.PackageCoveredAmount).HasPrecision(18, 2);
        builder.Property(item => item.SegmentStartAt).HasPrecision(0);
        builder.Property(item => item.SegmentEndAt).HasPrecision(0);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.DoctorServiceId, item.SegmentStartAt, item.SegmentEndAt })
            .HasDatabaseName("IX_AppointmentServices_Doctor_TimeRange");
        builder.HasIndex(item => new { item.AppointmentId, item.SequenceNumber })
            .HasDatabaseName("IX_AppointmentServices_Appointment_Sequence");

        builder.HasOne(item => item.Service).WithMany()
            .HasForeignKey(item => new { item.ServiceId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.DoctorService).WithMany()
            .HasForeignKey(item => new { item.DoctorServiceId, item.ServiceId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.ServiceId, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Discount).WithMany()
            .HasForeignKey(item => item.DiscountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.DiscountOverrideByAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Devices).WithOne(item => item.AppointmentService)
            .HasForeignKey(item => new { item.AppointmentServiceId, item.ServiceId, item.DepartmentId })
            .HasPrincipalKey(item => new { item.Id, item.ServiceId, item.DepartmentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PackageSessionBooking)
            .WithOne(item => item.AppointmentService)
            .HasForeignKey<PackageSessionBooking>(item =>
                new { item.AppointmentServiceId, item.AppointmentId, item.ServiceId })
            .HasPrincipalKey<AppointmentService>(item =>
                new { item.Id, item.AppointmentId, item.ServiceId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Devices).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
