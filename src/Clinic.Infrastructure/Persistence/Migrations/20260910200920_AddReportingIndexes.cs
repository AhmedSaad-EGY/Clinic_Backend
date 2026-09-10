using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorUserId",
                schema: "audit",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Status_ExecutedAt_Executor",
                schema: "cashier",
                table: "Refunds",
                columns: new[] { "Status", "ExecutedAt", "ExecutedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CollectedAt_Collector",
                schema: "cashier",
                table: "Payments",
                columns: new[] { "CollectedAt", "CollectedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashWithdrawals_Status_ExecutedAt_Executor",
                schema: "cashier",
                table: "CashWithdrawals",
                columns: new[] { "Status", "ExecutedAt", "ExecutedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_OccurredAt",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "Action", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Actor_OccurredAt",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StartAt",
                schema: "appointments",
                table: "Appointments",
                column: "StartAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Refunds_Status_ExecutedAt_Executor",
                schema: "cashier",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CollectedAt_Collector",
                schema: "cashier",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_CashWithdrawals_Status_ExecutedAt_Executor",
                schema: "cashier",
                table: "CashWithdrawals");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action_OccurredAt",
                schema: "audit",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Actor_OccurredAt",
                schema: "audit",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_StartAt",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                schema: "audit",
                table: "AuditLogs",
                column: "ActorUserId");
        }
    }
}
