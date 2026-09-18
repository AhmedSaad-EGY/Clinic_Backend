namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts", "cashier", table =>
        {
            table.HasCheckConstraint("CK_Shifts_Status", "[Status] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Shifts_TimeRange",
                "[ScheduledStart] < [ScheduledEnd] AND [ScheduledEnd] <= [GraceEndsAt]");
            table.HasCheckConstraint("CK_Shifts_OpeningBalance",
                "([OpeningBalance] IS NULL AND [OpeningBalanceEnteredByUserId] IS NULL " +
                "AND [OpeningBalanceEnteredAt] IS NULL) OR ([OpeningBalance] >= 0 " +
                "AND [OpeningBalanceEnteredByUserId] IS NOT NULL " +
                "AND [OpeningBalanceEnteredAt] IS NOT NULL)");
            table.HasCheckConstraint("CK_Shifts_Reconciliation",
                "([ExpectedCash] IS NULL AND [DeclaredCash] IS NULL AND [CashVariance] IS NULL) " +
                "OR ([DeclaredCash] >= 0 " +
                "AND [CashVariance] = [DeclaredCash] - [ExpectedCash])");
            table.HasCheckConstraint("CK_Shifts_TerminalState",
                "([Status] = 1 AND [ActualOpenedAt] IS NULL AND [OpeningBalance] IS NULL " +
                "AND [ExpectedCash] IS NULL AND [ClosedAt] IS NULL " +
                "AND [ClosedByUserId] IS NULL AND [CancelledAt] IS NULL " +
                "AND [CancelledByAdminUserId] IS NULL AND [CancellationReason] IS NULL) " +
                "OR ([Status] BETWEEN 2 AND 3 AND [ActualOpenedAt] IS NOT NULL " +
                "AND [ClosedAt] IS NULL AND [ClosedByUserId] IS NULL " +
                "AND [CancelledAt] IS NULL AND [CancelledByAdminUserId] IS NULL " +
                "AND [CancellationReason] IS NULL) OR ([Status] = 4 " +
                "AND [ActualOpenedAt] IS NOT NULL AND [OpeningBalance] IS NOT NULL " +
                "AND [ExpectedCash] IS NOT NULL AND [DeclaredCash] IS NOT NULL " +
                "AND [CashVariance] IS NOT NULL AND [ClosedAt] IS NOT NULL " +
                "AND [ClosedByUserId] IS NOT NULL AND [CancelledAt] IS NULL " +
                "AND [CancelledByAdminUserId] IS NULL AND [CancellationReason] IS NULL) " +
                "OR ([Status] = 5 AND [ActualOpenedAt] IS NULL " +
                "AND [OpeningBalance] IS NULL AND [ExpectedCash] IS NULL " +
                "AND [CancelledAt] IS NOT NULL AND [CancelledByAdminUserId] IS NOT NULL " +
                "AND [CancellationReason] IS NOT NULL AND [ClosedAt] IS NULL " +
                "AND [ClosedByUserId] IS NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.ScheduledStart).HasPrecision(0);
        builder.Property(item => item.ScheduledEnd).HasPrecision(0);
        builder.Property(item => item.GraceEndsAt).HasPrecision(0);
        builder.Property(item => item.ActualOpenedAt).HasPrecision(0);
        builder.Property(item => item.ClosedAt).HasPrecision(0);
        builder.Property(item => item.OpeningBalanceEnteredAt).HasPrecision(0);
        builder.Property(item => item.CancelledAt).HasPrecision(0);
        builder.Property(item => item.OpeningBalance).HasPrecision(18, 2);
        builder.Property(item => item.ExpectedCash).HasPrecision(18, 2);
        builder.Property(item => item.DeclaredCash).HasPrecision(18, 2);
        builder.Property(item => item.CashVariance).HasPrecision(18, 2);
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.CloseNote).HasMaxLength(500);
        builder.Property(item => item.CancellationReason).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.CashDrawerId, item.ScheduledStart })
            .IsUnique()
            .HasFilter("[Status] <> 5")
            .HasDatabaseName("UX_Shifts_Drawer_Start_Active");
        builder.HasIndex(item => new
        {
            item.CashDrawerId,
            item.ScheduledStart,
            item.GraceEndsAt
        }).HasDatabaseName("IX_Shifts_Drawer_TimeRange");
        builder.HasIndex(item => new { item.Status, item.ScheduledStart })
            .HasDatabaseName("IX_Shifts_Status_Start");

        builder.HasOne(item => item.CashDrawer).WithMany()
            .HasForeignKey(item => item.CashDrawerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.ScheduledByAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.OpeningBalanceEnteredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CancelledByAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
