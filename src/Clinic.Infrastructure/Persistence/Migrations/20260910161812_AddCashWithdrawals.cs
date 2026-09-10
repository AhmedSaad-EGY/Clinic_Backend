using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "CashWithdrawalNumberSequence",
                schema: "cashier");

            migrationBuilder.CreateTable(
                name: "CashWithdrawals",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WithdrawalNumber = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ShiftId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ReviewedByAdminUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExecutionIdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExecutedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CancelledByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashWithdrawals", x => x.Id);
                    table.CheckConstraint("CK_CashWithdrawals_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_CashWithdrawals_Lifecycle", "([Status] = 1 AND [ReviewedByAdminUserId] IS NULL AND [ReviewedAt] IS NULL AND [DecisionReason] IS NULL AND [ExecutionIdempotencyKey] IS NULL AND [ExecutedByUserId] IS NULL AND [ExecutedAt] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL) OR ([Status] IN (2, 3) AND [ReviewedByAdminUserId] IS NOT NULL AND [ReviewedAt] IS NOT NULL AND [DecisionReason] IS NOT NULL AND [ExecutionIdempotencyKey] IS NULL AND [ExecutedByUserId] IS NULL AND [ExecutedAt] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL) OR ([Status] = 4 AND [ReviewedByAdminUserId] IS NULL AND [ReviewedAt] IS NULL AND [DecisionReason] IS NULL AND [ExecutionIdempotencyKey] IS NULL AND [ExecutedByUserId] IS NULL AND [ExecutedAt] IS NULL AND [CancelledByUserId] IS NOT NULL AND [CancelledAt] IS NOT NULL) OR ([Status] = 5 AND [ReviewedByAdminUserId] IS NOT NULL AND [ReviewedAt] IS NOT NULL AND [DecisionReason] IS NOT NULL AND [ExecutionIdempotencyKey] IS NOT NULL AND [ExecutedByUserId] IS NOT NULL AND [ExecutedAt] IS NOT NULL AND [CancelledByUserId] IS NULL AND [CancelledAt] IS NULL)");
                    table.CheckConstraint("CK_CashWithdrawals_Status", "[Status] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_CashWithdrawals_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "cashier",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashWithdrawals_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashWithdrawals_Users_ExecutedByUserId",
                        column: x => x.ExecutedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashWithdrawals_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashWithdrawals_Users_ReviewedByAdminUserId",
                        column: x => x.ReviewedByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashWithdrawals_CancelledByUserId",
                schema: "cashier",
                table: "CashWithdrawals",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashWithdrawals_ExecutedByUserId",
                schema: "cashier",
                table: "CashWithdrawals",
                column: "ExecutedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashWithdrawals_RequestedByUserId",
                schema: "cashier",
                table: "CashWithdrawals",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashWithdrawals_ReviewedByAdminUserId",
                schema: "cashier",
                table: "CashWithdrawals",
                column: "ReviewedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashWithdrawals_Shift_Status_RequestedAt",
                schema: "cashier",
                table: "CashWithdrawals",
                columns: new[] { "ShiftId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_CashWithdrawals_ExecutionIdempotencyKey",
                schema: "cashier",
                table: "CashWithdrawals",
                column: "ExecutionIdempotencyKey",
                unique: true,
                filter: "[ExecutionIdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_CashWithdrawals_IdempotencyKey",
                schema: "cashier",
                table: "CashWithdrawals",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CashWithdrawals_Number",
                schema: "cashier",
                table: "CashWithdrawals",
                column: "WithdrawalNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashWithdrawals",
                schema: "cashier");

            migrationBuilder.DropSequence(
                name: "CashWithdrawalNumberSequence",
                schema: "cashier");
        }
    }
}
