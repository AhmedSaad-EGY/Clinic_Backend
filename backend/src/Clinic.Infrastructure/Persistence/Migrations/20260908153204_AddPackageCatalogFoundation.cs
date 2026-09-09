using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "packages");

            migrationBuilder.CreateTable(
                name: "Packages",
                schema: "packages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SessionCount = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByAdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByAdminUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                    table.UniqueConstraint("AK_Packages_Id_DepartmentId", x => new { x.Id, x.DepartmentId });
                    table.CheckConstraint("CK_Packages_BasePrice", "[BasePrice] >= 0");
                    table.CheckConstraint("CK_Packages_SessionCount", "[SessionCount] > 0 AND [SessionCount] <= 500");
                    table.ForeignKey(
                        name: "FK_Packages_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "catalog",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Packages_Users_CreatedByAdminUserId",
                        column: x => x.CreatedByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Packages_Users_UpdatedByAdminUserId",
                        column: x => x.UpdatedByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageServices",
                schema: "packages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    SessionsIncluded = table.Column<int>(type: "int", nullable: false),
                    UnitPriceAtDefinition = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageServices", x => x.Id);
                    table.CheckConstraint("CK_PackageServices_SessionsIncluded", "[SessionsIncluded] > 0");
                    table.CheckConstraint("CK_PackageServices_UnitPriceAtDefinition", "[UnitPriceAtDefinition] > 0");
                    table.ForeignKey(
                        name: "FK_PackageServices_Packages_PackageId_DepartmentId",
                        columns: x => new { x.PackageId, x.DepartmentId },
                        principalSchema: "packages",
                        principalTable: "Packages",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageServices_Services_ServiceId_DepartmentId",
                        columns: x => new { x.ServiceId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "Services",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_CreatedByAdminUserId",
                schema: "packages",
                table: "Packages",
                column: "CreatedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Department_Status",
                schema: "packages",
                table: "Packages",
                columns: new[] { "DepartmentId", "IsArchived", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_UpdatedByAdminUserId",
                schema: "packages",
                table: "Packages",
                column: "UpdatedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Packages_Name",
                schema: "packages",
                table: "Packages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageServices_PackageId_DepartmentId",
                schema: "packages",
                table: "PackageServices",
                columns: new[] { "PackageId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageServices_ServiceId_DepartmentId",
                schema: "packages",
                table: "PackageServices",
                columns: new[] { "ServiceId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "UX_PackageServices_PackageId_ServiceId",
                schema: "packages",
                table: "PackageServices",
                columns: new[] { "PackageId", "ServiceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageServices",
                schema: "packages");

            migrationBuilder.DropTable(
                name: "Packages",
                schema: "packages");
        }
    }
}
