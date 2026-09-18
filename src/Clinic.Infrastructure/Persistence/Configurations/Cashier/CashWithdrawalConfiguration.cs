namespace Clinic.Infrastructure.Persistence.Configurations.Cashier;

public sealed class CashWithdrawalConfiguration
    : IEntityTypeConfiguration<CashWithdrawal>
{
    public void Configure(EntityTypeBuilder<CashWithdrawal> builder)
    {
        builder.ToTable("CashWithdrawals", "cashier", table =>
        {
            table.HasCheckConstraint("CK_CashWithdrawals_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_CashWithdrawals_Status",
                "[Status] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_CashWithdrawals_Lifecycle",
                "([Status] = 1 AND [ReviewedByAdminUserId] IS NULL " +
                "AND [ReviewedAt] IS NULL AND [DecisionReason] IS NULL " +
                "AND [ExecutionIdempotencyKey] IS NULL AND [ExecutedByUserId] IS NULL " +
                "AND [ExecutedAt] IS NULL AND [CancelledByUserId] IS NULL " +
                "AND [CancelledAt] IS NULL) OR ([Status] IN (2, 3) " +
                "AND [ReviewedByAdminUserId] IS NOT NULL AND [ReviewedAt] IS NOT NULL " +
                "AND [DecisionReason] IS NOT NULL AND [ExecutionIdempotencyKey] IS NULL " +
                "AND [ExecutedByUserId] IS NULL AND [ExecutedAt] IS NULL " +
                "AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL) " +
                "OR ([Status] = 4 AND [ReviewedByAdminUserId] IS NULL " +
                "AND [ReviewedAt] IS NULL AND [DecisionReason] IS NULL " +
                "AND [ExecutionIdempotencyKey] IS NULL AND [ExecutedByUserId] IS NULL " +
                "AND [ExecutedAt] IS NULL AND [CancelledByUserId] IS NOT NULL " +
                "AND [CancelledAt] IS NOT NULL) OR ([Status] = 5 " +
                "AND [ReviewedByAdminUserId] IS NOT NULL AND [ReviewedAt] IS NOT NULL " +
                "AND [DecisionReason] IS NOT NULL AND [ExecutionIdempotencyKey] IS NOT NULL " +
                "AND [ExecutedByUserId] IS NOT NULL AND [ExecutedAt] IS NOT NULL " +
                "AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.WithdrawalNumber).HasMaxLength(40)
            .IsUnicode(false).IsRequired();
        builder.Property(item => item.RequestFingerprint).HasMaxLength(64)
            .IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.Reason).HasMaxLength(500).IsRequired();
        builder.Property(item => item.DecisionReason).HasMaxLength(500);
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.RequestedAt).HasPrecision(0);
        builder.Property(item => item.ReviewedAt).HasPrecision(0);
        builder.Property(item => item.ExecutedAt).HasPrecision(0);
        builder.Property(item => item.CancelledAt).HasPrecision(0);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => item.WithdrawalNumber).IsUnique()
            .HasDatabaseName("UX_CashWithdrawals_Number");
        builder.HasIndex(item => item.IdempotencyKey).IsUnique()
            .HasDatabaseName("UX_CashWithdrawals_IdempotencyKey");
        builder.HasIndex(item => item.ExecutionIdempotencyKey).IsUnique()
            .HasFilter("[ExecutionIdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_CashWithdrawals_ExecutionIdempotencyKey");
        builder.HasIndex(item => new { item.ShiftId, item.Status, item.RequestedAt })
            .HasDatabaseName("IX_CashWithdrawals_Shift_Status_RequestedAt");
        builder.HasIndex(item => new
            { item.Status, item.ExecutedAt, item.ExecutedByUserId })
            .HasDatabaseName("IX_CashWithdrawals_Status_ExecutedAt_Executor");

        builder.HasOne(item => item.Shift).WithMany()
            .HasForeignKey(item => item.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.ReviewedByAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.ExecutedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
