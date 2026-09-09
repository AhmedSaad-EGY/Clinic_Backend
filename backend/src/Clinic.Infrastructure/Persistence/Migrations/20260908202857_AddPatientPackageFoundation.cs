using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientPackageFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivationGraceDays",
                schema: "packages",
                table: "Packages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsageDurationDays",
                schema: "packages",
                table: "Packages",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PackageServices_Id_PackageId_ServiceId",
                schema: "packages",
                table: "PackageServices",
                columns: new[] { "Id", "PackageId", "ServiceId" });

            migrationBuilder.CreateTable(
                name: "PatientPackages",
                schema: "packages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    PackageNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TotalSessions = table.Column<int>(type: "int", nullable: false),
                    BasePriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActivationGraceDaysSnapshot = table.Column<int>(type: "int", nullable: false),
                    UsageDurationDaysSnapshot = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RegisteredByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ActivationWindowStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ActivationDeadlineAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    FirstUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    UpdatedByAdminUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientPackages", x => x.Id);
                    table.UniqueConstraint("AK_PatientPackages_Id_DepartmentId", x => new { x.Id, x.DepartmentId });
                    table.UniqueConstraint("AK_PatientPackages_Id_PackageId", x => new { x.Id, x.PackageId });
                    table.CheckConstraint("CK_PatientPackages_Durations", "[ActivationGraceDaysSnapshot] > 0 AND [UsageDurationDaysSnapshot] > 0");
                    table.CheckConstraint("CK_PatientPackages_PaymentStatus", "[PaymentStatus] IN (1, 2, 3)");
                    table.CheckConstraint("CK_PatientPackages_PaymentTimeline", "([PaymentStatus] = 1 AND [NetPriceSnapshot] > 0 AND [ActivationWindowStartedAt] IS NULL AND [ActivationDeadlineAt] IS NULL) OR ([PaymentStatus] = 2 AND [NetPriceSnapshot] = 0 AND [ActivationWindowStartedAt] IS NOT NULL AND [ActivationDeadlineAt] IS NOT NULL) OR ([PaymentStatus] = 3 AND [NetPriceSnapshot] > 0 AND [ActivationWindowStartedAt] IS NOT NULL AND [ActivationDeadlineAt] IS NOT NULL)");
                    table.CheckConstraint("CK_PatientPackages_Prices", "[BasePriceSnapshot] >= 0 AND [NetPriceSnapshot] >= 0");
                    table.CheckConstraint("CK_PatientPackages_Status", "[Status] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_PatientPackages_TotalSessions", "[TotalSessions] BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_PatientPackages_Packages_PackageId_DepartmentId",
                        columns: x => new { x.PackageId, x.DepartmentId },
                        principalSchema: "packages",
                        principalTable: "Packages",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientPackages_Patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "patients",
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientPackages_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientPackages_Users_UpdatedByAdminUserId",
                        column: x => x.UpdatedByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientPackageServices",
                schema: "packages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientPackageId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    SourcePackageServiceId = table.Column<long>(type: "bigint", nullable: false),
                    SourcePackageId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SpecializationIdSnapshot = table.Column<long>(type: "bigint", nullable: false),
                    SpecializationNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SessionsPurchased = table.Column<int>(type: "int", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientPackageServices", x => x.Id);
                    table.UniqueConstraint("AK_PatientPackageServices_Id_PatientPackageId_ServiceId", x => new { x.Id, x.PatientPackageId, x.ServiceId });
                    table.CheckConstraint("CK_PatientPackageServices_SessionsPurchased", "[SessionsPurchased] > 0");
                    table.CheckConstraint("CK_PatientPackageServices_UnitPriceSnapshot", "[UnitPriceSnapshot] > 0");
                    table.ForeignKey(
                        name: "FK_PatientPackageServices_PackageServices_SourcePackageServiceId_SourcePackageId_ServiceId",
                        columns: x => new { x.SourcePackageServiceId, x.SourcePackageId, x.ServiceId },
                        principalSchema: "packages",
                        principalTable: "PackageServices",
                        principalColumns: new[] { "Id", "PackageId", "ServiceId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientPackageServices_PatientPackages_PatientPackageId_DepartmentId",
                        columns: x => new { x.PatientPackageId, x.DepartmentId },
                        principalSchema: "packages",
                        principalTable: "PatientPackages",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientPackageServices_Services_ServiceId_DepartmentId",
                        columns: x => new { x.ServiceId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "Services",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageSessions",
                schema: "packages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientPackageServiceId = table.Column<long>(type: "bigint", nullable: false),
                    PatientPackageId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageSessions", x => x.Id);
                    table.CheckConstraint("CK_PackageSessions_SequenceNumber", "[SequenceNumber] > 0");
                    table.CheckConstraint("CK_PackageSessions_Status", "[Status] IN (1, 2, 3)");
                    table.CheckConstraint("CK_PackageSessions_StatusTimeline", "([Status] = 1 AND [ReservedAt] IS NULL AND [ConsumedAt] IS NULL) OR ([Status] = 2 AND [ReservedAt] IS NOT NULL AND [ConsumedAt] IS NULL) OR ([Status] = 3 AND [ConsumedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_PackageSessions_UnitPriceSnapshot", "[UnitPriceSnapshot] > 0");
                    table.ForeignKey(
                        name: "FK_PackageSessions_PatientPackageServices_PatientPackageServiceId_PatientPackageId_ServiceId",
                        columns: x => new { x.PatientPackageServiceId, x.PatientPackageId, x.ServiceId },
                        principalSchema: "packages",
                        principalTable: "PatientPackageServices",
                        principalColumns: new[] { "Id", "PatientPackageId", "ServiceId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Packages_ActivationGraceDays",
                schema: "packages",
                table: "Packages",
                sql: "[ActivationGraceDays] IS NULL OR [ActivationGraceDays] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Packages_UsageDurationDays",
                schema: "packages",
                table: "Packages",
                sql: "[UsageDurationDays] IS NULL OR [UsageDurationDays] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PackageSessions_PatientPackage_Status",
                schema: "packages",
                table: "PackageSessions",
                columns: new[] { "PatientPackageId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageSessions_PatientPackageServiceId_PatientPackageId_ServiceId",
                schema: "packages",
                table: "PackageSessions",
                columns: new[] { "PatientPackageServiceId", "PatientPackageId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "UX_PackageSessions_Service_Sequence",
                schema: "packages",
                table: "PackageSessions",
                columns: new[] { "PatientPackageServiceId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackages_Department_Status",
                schema: "packages",
                table: "PatientPackages",
                columns: new[] { "DepartmentId", "Status", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackages_PackageId_DepartmentId",
                schema: "packages",
                table: "PatientPackages",
                columns: new[] { "PackageId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackages_Patient_RegisteredAt",
                schema: "packages",
                table: "PatientPackages",
                columns: new[] { "PatientId", "RegisteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackages_RegisteredByUserId",
                schema: "packages",
                table: "PatientPackages",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackages_UpdatedByAdminUserId",
                schema: "packages",
                table: "PatientPackages",
                column: "UpdatedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "UX_PatientPackages_IdempotencyKey",
                schema: "packages",
                table: "PatientPackages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackageServices_PatientPackageId_DepartmentId",
                schema: "packages",
                table: "PatientPackageServices",
                columns: new[] { "PatientPackageId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackageServices_ServiceId_DepartmentId",
                schema: "packages",
                table: "PatientPackageServices",
                columns: new[] { "ServiceId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackageServices_SourcePackageServiceId_SourcePackageId_ServiceId",
                schema: "packages",
                table: "PatientPackageServices",
                columns: new[] { "SourcePackageServiceId", "SourcePackageId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "UX_PatientPackageServices_Package_Service",
                schema: "packages",
                table: "PatientPackageServices",
                columns: new[] { "PatientPackageId", "ServiceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageSessions",
                schema: "packages");

            migrationBuilder.DropTable(
                name: "PatientPackageServices",
                schema: "packages");

            migrationBuilder.DropTable(
                name: "PatientPackages",
                schema: "packages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PackageServices_Id_PackageId_ServiceId",
                schema: "packages",
                table: "PackageServices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Packages_ActivationGraceDays",
                schema: "packages",
                table: "Packages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Packages_UsageDurationDays",
                schema: "packages",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "ActivationGraceDays",
                schema: "packages",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "UsageDurationDays",
                schema: "packages",
                table: "Packages");
        }
    }
}
