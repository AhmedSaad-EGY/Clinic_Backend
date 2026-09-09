using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "PaymentTransactionNumberSequence",
                schema: "cashier");

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsCash = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                    table.CheckConstraint("CK_PaymentMethods_SortOrder", "[SortOrder] > 0");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionNumber = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ShiftId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CollectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.UniqueConstraint("AK_Payments_Id_PatientId", x => new { x.Id, x.PatientId });
                    table.CheckConstraint("CK_Payments_Status", "[Status] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_Payments_TotalAmount", "[TotalAmount] > 0");
                    table.ForeignKey(
                        name: "FK_Payments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "patients",
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "cashier",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Users_CollectedByUserId",
                        column: x => x.CollectedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentPaymentAllocations",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentPaymentAllocations", x => x.Id);
                    table.UniqueConstraint("AK_AppointmentPaymentAllocations_Id_PaymentId", x => new { x.Id, x.PaymentId });
                    table.CheckConstraint("CK_AppointmentPaymentAllocations_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_AppointmentPaymentAllocations_Appointments_AppointmentId_PatientId",
                        columns: x => new { x.AppointmentId, x.PatientId },
                        principalSchema: "appointments",
                        principalTable: "Appointments",
                        principalColumns: new[] { "Id", "PatientId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentPaymentAllocations_Payments_PaymentId_PatientId",
                        columns: x => new { x.PaymentId, x.PatientId },
                        principalSchema: "cashier",
                        principalTable: "Payments",
                        principalColumns: new[] { "Id", "PatientId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethodAllocations",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentMethodId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethodAllocations", x => x.Id);
                    table.UniqueConstraint("AK_PaymentMethodAllocations_Id_PaymentId", x => new { x.Id, x.PaymentId });
                    table.CheckConstraint("CK_PaymentMethodAllocations_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentMethodAllocations_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalSchema: "cashier",
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentMethodAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "cashier",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "cashier",
                table: "PaymentMethods",
                columns: new[] { "Id", "Code", "DisplayName", "IsActive", "IsCash", "SortOrder" },
                values: new object[,]
                {
                    { 1L, "CASH", "نقدي", true, true, 1 },
                    { 2L, "VISA", "Visa", true, false, 2 },
                    { 3L, "INSTAPAY", "InstaPay", true, false, 3 },
                    { 4L, "WALLET", "محفظة", true, false, 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentPaymentAllocations_AppointmentId_PatientId",
                schema: "cashier",
                table: "AppointmentPaymentAllocations",
                columns: new[] { "AppointmentId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentPaymentAllocations_PaymentId_PatientId",
                schema: "cashier",
                table: "AppointmentPaymentAllocations",
                columns: new[] { "PaymentId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "UX_AppointmentPaymentAllocations_AppointmentId",
                schema: "cashier",
                table: "AppointmentPaymentAllocations",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AppointmentPaymentAllocations_Payment_Appointment",
                schema: "cashier",
                table: "AppointmentPaymentAllocations",
                columns: new[] { "PaymentId", "AppointmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethodAllocations_PaymentMethodId",
                schema: "cashier",
                table: "PaymentMethodAllocations",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentMethodAllocations_Payment_Method",
                schema: "cashier",
                table: "PaymentMethodAllocations",
                columns: new[] { "PaymentId", "PaymentMethodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_SortOrder",
                schema: "cashier",
                table: "PaymentMethods",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentMethods_Code",
                schema: "cashier",
                table: "PaymentMethods",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CollectedByUserId",
                schema: "cashier",
                table: "Payments",
                column: "CollectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Patient_CollectedAt",
                schema: "cashier",
                table: "Payments",
                columns: new[] { "PatientId", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Shift_CollectedAt",
                schema: "cashier",
                table: "Payments",
                columns: new[] { "ShiftId", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Payments_IdempotencyKey",
                schema: "cashier",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Payments_TransactionNumber",
                schema: "cashier",
                table: "Payments",
                column: "TransactionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentPaymentAllocations",
                schema: "cashier");

            migrationBuilder.DropTable(
                name: "PaymentMethodAllocations",
                schema: "cashier");

            migrationBuilder.DropTable(
                name: "PaymentMethods",
                schema: "cashier");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "cashier");

            migrationBuilder.DropSequence(
                name: "PaymentTransactionNumberSequence",
                schema: "cashier");
        }
    }
}
