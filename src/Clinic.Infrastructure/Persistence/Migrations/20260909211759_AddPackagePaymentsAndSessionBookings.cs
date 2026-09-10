using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPackagePaymentsAndSessionBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentServices_Amounts",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_Amounts",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_PaymentStatus",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.AddColumn<decimal>(
                name: "PackageCoveredAmount",
                schema: "appointments",
                table: "AppointmentServices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                schema: "appointments",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageCoveredAmount",
                schema: "appointments",
                table: "Appointments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "PatientPackageId",
                schema: "appointments",
                table: "Appointments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                schema: "appointments",
                table: "Appointments",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PatientPackages_Id_PatientId",
                schema: "packages",
                table: "PatientPackages",
                columns: new[] { "Id", "PatientId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PackageSessions_Id_PatientPackageId_ServiceId",
                schema: "packages",
                table: "PackageSessions",
                columns: new[] { "Id", "PatientPackageId", "ServiceId" });

            migrationBuilder.CreateTable(
                name: "PackagePaymentAllocations",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    PatientPackageId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagePaymentAllocations", x => x.Id);
                    table.UniqueConstraint("AK_PackagePaymentAllocations_Id_PaymentId", x => new { x.Id, x.PaymentId });
                    table.CheckConstraint("CK_PackagePaymentAllocations_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_PackagePaymentAllocations_PatientPackages_PatientPackageId_PatientId",
                        columns: x => new { x.PatientPackageId, x.PatientId },
                        principalSchema: "packages",
                        principalTable: "PatientPackages",
                        principalColumns: new[] { "Id", "PatientId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackagePaymentAllocations_Payments_PaymentId_PatientId",
                        columns: x => new { x.PaymentId, x.PatientId },
                        principalSchema: "cashier",
                        principalTable: "Payments",
                        principalColumns: new[] { "Id", "PatientId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageSessionBookings",
                schema: "packages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackageSessionId = table.Column<long>(type: "bigint", nullable: false),
                    PatientPackageId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    AppointmentServiceId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageSessionBookings", x => x.Id);
                    table.CheckConstraint("CK_PackageSessionBookings_Status", "[Status] IN (1, 2, 3)");
                    table.CheckConstraint("CK_PackageSessionBookings_Timeline", "([Status] = 1 AND [ReleasedAt] IS NULL AND [ConsumedAt] IS NULL) OR ([Status] = 2 AND [ReleasedAt] IS NOT NULL AND [ConsumedAt] IS NULL) OR ([Status] = 3 AND [ReleasedAt] IS NULL AND [ConsumedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PackageSessionBookings_AppointmentServices_AppointmentServiceId_ServiceId",
                        columns: x => new { x.AppointmentServiceId, x.ServiceId },
                        principalSchema: "appointments",
                        principalTable: "AppointmentServices",
                        principalColumns: new[] { "Id", "ServiceId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageSessionBookings_Appointments_AppointmentId_PatientId",
                        columns: x => new { x.AppointmentId, x.PatientId },
                        principalSchema: "appointments",
                        principalTable: "Appointments",
                        principalColumns: new[] { "Id", "PatientId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageSessionBookings_PackageSessions_PackageSessionId_PatientPackageId_ServiceId",
                        columns: x => new { x.PackageSessionId, x.PatientPackageId, x.ServiceId },
                        principalSchema: "packages",
                        principalTable: "PackageSessions",
                        principalColumns: new[] { "Id", "PatientPackageId", "ServiceId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentServices_Amounts",
                schema: "appointments",
                table: "AppointmentServices",
                sql: "[UnitPrice] > 0 AND [GrossAmount] = [UnitPrice] * [Quantity] AND [DiscountAmount] >= 0 AND [PackageCoveredAmount] >= 0 AND [NetAmount] = [GrossAmount] - [DiscountAmount] - [PackageCoveredAmount]");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientPackageId_PatientId",
                schema: "appointments",
                table: "Appointments",
                columns: new[] { "PatientPackageId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_Package_IdempotencyKey",
                schema: "appointments",
                table: "Appointments",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_Amounts",
                schema: "appointments",
                table: "Appointments",
                sql: "[SubtotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [PackageCoveredAmount] >= 0 AND [NetAmount] >= 0 AND [NetAmount] = [SubtotalAmount] - [DiscountAmount] - [PackageCoveredAmount]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_PackageLink",
                schema: "appointments",
                table: "Appointments",
                sql: "([PatientPackageId] IS NULL AND [PackageCoveredAmount] = 0 AND [IdempotencyKey] IS NULL AND [RequestFingerprint] IS NULL) OR ([PatientPackageId] IS NOT NULL AND [PackageCoveredAmount] > 0 AND [PaymentStatus] = 5 AND [IdempotencyKey] IS NOT NULL AND LEN([RequestFingerprint]) = 64)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_PaymentStatus",
                schema: "appointments",
                table: "Appointments",
                sql: "[PaymentStatus] IN (1, 2, 3, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePaymentAllocations_PatientPackageId_PatientId",
                schema: "cashier",
                table: "PackagePaymentAllocations",
                columns: new[] { "PatientPackageId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_PackagePaymentAllocations_PaymentId_PatientId",
                schema: "cashier",
                table: "PackagePaymentAllocations",
                columns: new[] { "PaymentId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "UX_PackagePaymentAllocations_PatientPackageId",
                schema: "cashier",
                table: "PackagePaymentAllocations",
                column: "PatientPackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PackagePaymentAllocations_Payment_PatientPackage",
                schema: "cashier",
                table: "PackagePaymentAllocations",
                columns: new[] { "PaymentId", "PatientPackageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageSessionBookings_AppointmentId_PatientId",
                schema: "packages",
                table: "PackageSessionBookings",
                columns: new[] { "AppointmentId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageSessionBookings_AppointmentServiceId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings",
                columns: new[] { "AppointmentServiceId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageSessionBookings_PackageSessionId_PatientPackageId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings",
                columns: new[] { "PackageSessionId", "PatientPackageId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "UX_PackageSessionBookings_ReservedSession",
                schema: "packages",
                table: "PackageSessionBookings",
                column: "PackageSessionId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_PatientPackages_PatientPackageId_PatientId",
                schema: "appointments",
                table: "Appointments",
                columns: new[] { "PatientPackageId", "PatientId" },
                principalSchema: "packages",
                principalTable: "PatientPackages",
                principalColumns: new[] { "Id", "PatientId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_PatientPackages_PatientPackageId_PatientId",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropTable(
                name: "PackagePaymentAllocations",
                schema: "cashier");

            migrationBuilder.DropTable(
                name: "PackageSessionBookings",
                schema: "packages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PatientPackages_Id_PatientId",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PackageSessions_Id_PatientPackageId_ServiceId",
                schema: "packages",
                table: "PackageSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentServices_Amounts",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_PatientPackageId_PatientId",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "UX_Appointments_Package_IdempotencyKey",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_Amounts",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_PackageLink",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_PaymentStatus",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PackageCoveredAmount",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PackageCoveredAmount",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PatientPackageId",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentServices_Amounts",
                schema: "appointments",
                table: "AppointmentServices",
                sql: "[UnitPrice] > 0 AND [GrossAmount] = [UnitPrice] * [Quantity] AND [DiscountAmount] >= 0 AND [NetAmount] = [GrossAmount] - [DiscountAmount]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_Amounts",
                schema: "appointments",
                table: "Appointments",
                sql: "[SubtotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [NetAmount] >= 0 AND [NetAmount] = [SubtotalAmount] - [DiscountAmount]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_PaymentStatus",
                schema: "appointments",
                table: "Appointments",
                sql: "[PaymentStatus] IN (1, 2, 3, 4)");
        }
    }
}
