using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationApprovalsAndRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "RefundTransactionNumberSequence",
                schema: "cashier");

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalPaymentId = table.Column<long>(type: "bigint", nullable: true),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RequestNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByAdminUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                    table.CheckConstraint("CK_ApprovalRequests_RefundTarget", "([OriginalPaymentId] IS NULL AND [RequestedAmount] IS NULL) OR ([OriginalPaymentId] IS NOT NULL AND [RequestedAmount] > 0)");
                    table.CheckConstraint("CK_ApprovalRequests_Review", "([Status] = 1 AND [ReviewedByAdminUserId] IS NULL AND [ReviewedAt] IS NULL AND [DecisionReason] IS NULL) OR ([Status] IN (2, 3) AND [ReviewedByAdminUserId] IS NOT NULL AND [ReviewedAt] IS NOT NULL AND [DecisionReason] IS NOT NULL) OR [Status] = 4");
                    table.CheckConstraint("CK_ApprovalRequests_Status", "[Status] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_ApprovalRequests_Type", "[RequestType] = 1");
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "appointments",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_Payments_OriginalPaymentId",
                        column: x => x.OriginalPaymentId,
                        principalSchema: "cashier",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_Users_ReviewedByAdminUserId",
                        column: x => x.ReviewedByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionNumber = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalPaymentId = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionShiftId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExecutedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.UniqueConstraint("AK_Refunds_Id_OriginalPaymentId", x => new { x.Id, x.OriginalPaymentId });
                    table.CheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Refunds_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Refunds_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "cashier",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_OriginalPaymentId",
                        column: x => x.OriginalPaymentId,
                        principalSchema: "cashier",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Shifts_ExecutionShiftId",
                        column: x => x.ExecutionShiftId,
                        principalSchema: "cashier",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Users_ExecutedByUserId",
                        column: x => x.ExecutedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundAppointmentAllocations",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RefundId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalAllocationId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalPaymentId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundAppointmentAllocations", x => x.Id);
                    table.CheckConstraint("CK_RefundAppointmentAllocations_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_RefundAppointmentAllocations_AppointmentPaymentAllocations_OriginalAllocationId_OriginalPaymentId",
                        columns: x => new { x.OriginalAllocationId, x.OriginalPaymentId },
                        principalSchema: "cashier",
                        principalTable: "AppointmentPaymentAllocations",
                        principalColumns: new[] { "Id", "PaymentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundAppointmentAllocations_Refunds_RefundId_OriginalPaymentId",
                        columns: x => new { x.RefundId, x.OriginalPaymentId },
                        principalSchema: "cashier",
                        principalTable: "Refunds",
                        principalColumns: new[] { "Id", "OriginalPaymentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundMethodAllocations",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RefundId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalAllocationId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalPaymentId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundMethodAllocations", x => x.Id);
                    table.CheckConstraint("CK_RefundMethodAllocations_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_RefundMethodAllocations_PaymentMethodAllocations_OriginalAllocationId_OriginalPaymentId",
                        columns: x => new { x.OriginalAllocationId, x.OriginalPaymentId },
                        principalSchema: "cashier",
                        principalTable: "PaymentMethodAllocations",
                        principalColumns: new[] { "Id", "PaymentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundMethodAllocations_Refunds_RefundId_OriginalPaymentId",
                        columns: x => new { x.RefundId, x.OriginalPaymentId },
                        principalSchema: "cashier",
                        principalTable: "Refunds",
                        principalColumns: new[] { "Id", "OriginalPaymentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_OriginalPaymentId",
                schema: "cashier",
                table: "ApprovalRequests",
                column: "OriginalPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_RequestedByUserId",
                schema: "cashier",
                table: "ApprovalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_ReviewedByAdminUserId",
                schema: "cashier",
                table: "ApprovalRequests",
                column: "ReviewedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Status_RequestedAt",
                schema: "cashier",
                table: "ApprovalRequests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ApprovalRequests_ActiveAppointment",
                schema: "cashier",
                table: "ApprovalRequests",
                column: "AppointmentId",
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_RefundAppointmentAllocations_OriginalAllocationId_OriginalPaymentId",
                schema: "cashier",
                table: "RefundAppointmentAllocations",
                columns: new[] { "OriginalAllocationId", "OriginalPaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_RefundAppointmentAllocations_RefundId_OriginalPaymentId",
                schema: "cashier",
                table: "RefundAppointmentAllocations",
                columns: new[] { "RefundId", "OriginalPaymentId" });

            migrationBuilder.CreateIndex(
                name: "UX_RefundAppointmentAllocations_OriginalAllocationId",
                schema: "cashier",
                table: "RefundAppointmentAllocations",
                column: "OriginalAllocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundMethodAllocations_OriginalAllocationId_OriginalPaymentId",
                schema: "cashier",
                table: "RefundMethodAllocations",
                columns: new[] { "OriginalAllocationId", "OriginalPaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_RefundMethodAllocations_RefundId_OriginalPaymentId",
                schema: "cashier",
                table: "RefundMethodAllocations",
                columns: new[] { "RefundId", "OriginalPaymentId" });

            migrationBuilder.CreateIndex(
                name: "UX_RefundMethodAllocations_Refund_Original",
                schema: "cashier",
                table: "RefundMethodAllocations",
                columns: new[] { "RefundId", "OriginalAllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ExecutedByUserId",
                schema: "cashier",
                table: "Refunds",
                column: "ExecutedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OriginalPaymentId",
                schema: "cashier",
                table: "Refunds",
                column: "OriginalPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Shift_ExecutedAt",
                schema: "cashier",
                table: "Refunds",
                columns: new[] { "ExecutionShiftId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Refunds_ApprovalRequestId",
                schema: "cashier",
                table: "Refunds",
                column: "ApprovalRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Refunds_IdempotencyKey",
                schema: "cashier",
                table: "Refunds",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Refunds_TransactionNumber",
                schema: "cashier",
                table: "Refunds",
                column: "TransactionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefundAppointmentAllocations",
                schema: "cashier");

            migrationBuilder.DropTable(
                name: "RefundMethodAllocations",
                schema: "cashier");

            migrationBuilder.DropTable(
                name: "Refunds",
                schema: "cashier");

            migrationBuilder.DropTable(
                name: "ApprovalRequests",
                schema: "cashier");

            migrationBuilder.DropSequence(
                name: "RefundTransactionNumberSequence",
                schema: "cashier");
        }
    }
}
