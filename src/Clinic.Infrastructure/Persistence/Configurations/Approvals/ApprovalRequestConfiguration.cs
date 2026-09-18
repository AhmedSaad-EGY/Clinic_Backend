namespace Clinic.Infrastructure.Persistence.Configurations.Approvals;

public sealed class ApprovalRequestConfiguration
    : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("ApprovalRequests", "cashier", table =>
        {
            table.HasCheckConstraint("CK_ApprovalRequests_Type", "[RequestType] = 1");
            table.HasCheckConstraint("CK_ApprovalRequests_Status",
                "[Status] BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_ApprovalRequests_RefundTarget",
                "([OriginalPaymentId] IS NULL AND [RequestedAmount] IS NULL) OR " +
                "([OriginalPaymentId] IS NOT NULL AND [RequestedAmount] > 0)");
            table.HasCheckConstraint("CK_ApprovalRequests_Review",
                "([Status] = 1 AND [ReviewedByAdminUserId] IS NULL AND " +
                "[ReviewedAt] IS NULL AND [DecisionReason] IS NULL) OR " +
                "([Status] IN (2, 3) AND [ReviewedByAdminUserId] IS NOT NULL AND " +
                "[ReviewedAt] IS NOT NULL AND [DecisionReason] IS NOT NULL) OR [Status] = 4");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.RequestType).HasConversion<int>().IsRequired();
        builder.Property(item => item.RequestedAmount).HasPrecision(18, 2);
        builder.Property(item => item.RequestedAt).HasPrecision(0);
        builder.Property(item => item.RequestNote).HasMaxLength(500).IsRequired();
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.ReviewedAt).HasPrecision(0);
        builder.Property(item => item.DecisionReason).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.AppointmentId)
            .HasFilter("[Status] IN (1, 2)").IsUnique()
            .HasDatabaseName("UX_ApprovalRequests_ActiveAppointment");
        builder.HasIndex(item => new { item.Status, item.RequestedAt })
            .HasDatabaseName("IX_ApprovalRequests_Status_RequestedAt");

        builder.HasOne<Appointment>().WithMany()
            .HasForeignKey(item => item.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany()
            .HasForeignKey(item => item.OriginalPaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.ReviewedByAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
