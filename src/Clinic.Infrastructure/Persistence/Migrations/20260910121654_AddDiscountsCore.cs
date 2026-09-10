using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientPackages_Prices",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_PaymentStatus",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.EnsureSchema(
                name: "discounts");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmountSnapshot",
                schema: "packages",
                table: "PatientPackages",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "DiscountId",
                schema: "packages",
                table: "PatientPackages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DiscountId",
                schema: "appointments",
                table: "AppointmentServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DiscountOverrideByAdminUserId",
                schema: "appointments",
                table: "AppointmentServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountOverrideMode",
                schema: "appointments",
                table: "AppointmentServices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountOverrideReason",
                schema: "appointments",
                table: "AppointmentServices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Discounts",
                schema: "discounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliesTo = table.Column<int>(type: "int", nullable: false),
                    ScopeMode = table.Column<int>(type: "int", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByAdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UpdatedByAdminUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discounts", x => x.Id);
                    table.CheckConstraint("CK_Discounts_AppliesTo", "[AppliesTo] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Discounts_Archive", "[IsArchived] = 0 OR [IsActive] = 0");
                    table.CheckConstraint("CK_Discounts_Period", "[StartAt] < [EndAt]");
                    table.CheckConstraint("CK_Discounts_ScopeMode", "[ScopeMode] IN (1, 2)");
                    table.CheckConstraint("CK_Discounts_Type", "[Type] IN (1, 2)");
                    table.CheckConstraint("CK_Discounts_Value", "([Type] = 1 AND [Value] BETWEEN 0 AND 100) OR ([Type] = 2 AND [Value] > 0)");
                    table.ForeignKey(
                        name: "FK_Discounts_Users_CreatedByAdminUserId",
                        column: x => x.CreatedByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Discounts_Users_UpdatedByAdminUserId",
                        column: x => x.UpdatedByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountDepartments",
                schema: "discounts",
                columns: table => new
                {
                    DiscountId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountDepartments", x => new { x.DiscountId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_DiscountDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "catalog",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscountDepartments_Discounts_DiscountId",
                        column: x => x.DiscountId,
                        principalSchema: "discounts",
                        principalTable: "Discounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountPackages",
                schema: "discounts",
                columns: table => new
                {
                    DiscountId = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountPackages", x => new { x.DiscountId, x.PackageId });
                    table.ForeignKey(
                        name: "FK_DiscountPackages_Discounts_DiscountId",
                        column: x => x.DiscountId,
                        principalSchema: "discounts",
                        principalTable: "Discounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscountPackages_Packages_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "packages",
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountServices",
                schema: "discounts",
                columns: table => new
                {
                    DiscountId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountServices", x => new { x.DiscountId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_DiscountServices_Discounts_DiscountId",
                        column: x => x.DiscountId,
                        principalSchema: "discounts",
                        principalTable: "Discounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscountServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "catalog",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackages_DiscountId",
                schema: "packages",
                table: "PatientPackages",
                column: "DiscountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientPackages_Prices",
                schema: "packages",
                table: "PatientPackages",
                sql: "[BasePriceSnapshot] >= 0 AND [DiscountAmountSnapshot] >= 0 AND [NetPriceSnapshot] = [BasePriceSnapshot] - [DiscountAmountSnapshot] AND ([DiscountId] IS NOT NULL OR [DiscountAmountSnapshot] = 0)");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_DiscountId",
                schema: "appointments",
                table: "AppointmentServices",
                column: "DiscountId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_DiscountOverrideByAdminUserId",
                schema: "appointments",
                table: "AppointmentServices",
                column: "DiscountOverrideByAdminUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentServices_Discount",
                schema: "appointments",
                table: "AppointmentServices",
                sql: "([DiscountId] IS NOT NULL OR [DiscountAmount] = 0) AND ([DiscountOverrideMode] IS NULL AND [DiscountOverrideByAdminUserId] IS NULL AND [DiscountOverrideReason] IS NULL OR [DiscountOverrideMode] = 1 AND [DiscountId] IS NOT NULL AND [DiscountOverrideByAdminUserId] IS NOT NULL AND LEN([DiscountOverrideReason]) BETWEEN 1 AND 500 OR [DiscountOverrideMode] = 2 AND [DiscountId] IS NULL AND [DiscountAmount] = 0 AND [DiscountOverrideByAdminUserId] IS NOT NULL AND LEN([DiscountOverrideReason]) BETWEEN 1 AND 500)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_PaymentStatus",
                schema: "appointments",
                table: "Appointments",
                sql: "[PaymentStatus] IN (1, 2, 3, 4, 5, 6)");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountDepartments_DepartmentId",
                schema: "discounts",
                table: "DiscountDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountPackages_PackageId",
                schema: "discounts",
                table: "DiscountPackages",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_AppliesTo_ScopeMode",
                schema: "discounts",
                table: "Discounts",
                columns: new[] { "AppliesTo", "ScopeMode" });

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_CreatedByAdminUserId",
                schema: "discounts",
                table: "Discounts",
                column: "CreatedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_EffectivePeriod",
                schema: "discounts",
                table: "Discounts",
                columns: new[] { "IsActive", "IsArchived", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_UpdatedByAdminUserId",
                schema: "discounts",
                table: "Discounts",
                column: "UpdatedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountServices_ServiceId",
                schema: "discounts",
                table: "DiscountServices",
                column: "ServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentServices_Discounts_DiscountId",
                schema: "appointments",
                table: "AppointmentServices",
                column: "DiscountId",
                principalSchema: "discounts",
                principalTable: "Discounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentServices_Users_DiscountOverrideByAdminUserId",
                schema: "appointments",
                table: "AppointmentServices",
                column: "DiscountOverrideByAdminUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPackages_Discounts_DiscountId",
                schema: "packages",
                table: "PatientPackages",
                column: "DiscountId",
                principalSchema: "discounts",
                principalTable: "Discounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentServices_Discounts_DiscountId",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentServices_Users_DiscountOverrideByAdminUserId",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPackages_Discounts_DiscountId",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropTable(
                name: "DiscountDepartments",
                schema: "discounts");

            migrationBuilder.DropTable(
                name: "DiscountPackages",
                schema: "discounts");

            migrationBuilder.DropTable(
                name: "DiscountServices",
                schema: "discounts");

            migrationBuilder.DropTable(
                name: "Discounts",
                schema: "discounts");

            migrationBuilder.DropIndex(
                name: "IX_PatientPackages_DiscountId",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientPackages_Prices",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentServices_DiscountId",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentServices_DiscountOverrideByAdminUserId",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentServices_Discount",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_PaymentStatus",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DiscountAmountSnapshot",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropColumn(
                name: "DiscountId",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropColumn(
                name: "DiscountId",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "DiscountOverrideByAdminUserId",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "DiscountOverrideMode",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "DiscountOverrideReason",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientPackages_Prices",
                schema: "packages",
                table: "PatientPackages",
                sql: "[BasePriceSnapshot] >= 0 AND [NetPriceSnapshot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_PaymentStatus",
                schema: "appointments",
                table: "Appointments",
                sql: "[PaymentStatus] IN (1, 2, 3, 4, 5)");
        }
    }
}
