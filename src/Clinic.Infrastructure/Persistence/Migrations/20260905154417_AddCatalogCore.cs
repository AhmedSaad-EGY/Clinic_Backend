using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.CheckConstraint("CK_Departments_Status", "[Status] IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.UniqueConstraint("AK_Devices_Id_DepartmentId", x => new { x.Id, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_Devices_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "catalog",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.UniqueConstraint("AK_Rooms_Id_DepartmentId", x => new { x.Id, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_Rooms_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "catalog",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Specializations",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specializations", x => x.Id);
                    table.UniqueConstraint("AK_Specializations_Id_DepartmentId", x => new { x.Id, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_Specializations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "catalog",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    SpecializationId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServiceType = table.Column<int>(type: "int", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    PricingMode = table.Column<int>(type: "int", nullable: false),
                    CurrentUnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.UniqueConstraint("AK_Services_Id_DepartmentId", x => new { x.Id, x.DepartmentId });
                    table.CheckConstraint("CK_Services_CurrentUnitPrice", "[CurrentUnitPrice] > 0");
                    table.CheckConstraint("CK_Services_DurationMinutes", "[DurationMinutes] > 0 AND [DurationMinutes] <= 1440");
                    table.CheckConstraint("CK_Services_PricingMode", "[PricingMode] IN (1, 2)");
                    table.CheckConstraint("CK_Services_ServiceType", "[ServiceType] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Services_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "catalog",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Services_Specializations_SpecializationId_DepartmentId",
                        columns: x => new { x.SpecializationId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "Specializations",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDevices",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDevices", x => x.Id);
                    table.UniqueConstraint("AK_ServiceDevices_Id_ServiceId_DepartmentId", x => new { x.Id, x.ServiceId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_ServiceDevices_Devices_DeviceId_DepartmentId",
                        columns: x => new { x.DeviceId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "Devices",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceDevices_Services_ServiceId_DepartmentId",
                        columns: x => new { x.ServiceId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "Services",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServicePriceHistory",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePriceHistory", x => x.Id);
                    table.CheckConstraint("CK_ServicePriceHistory_Period", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                    table.CheckConstraint("CK_ServicePriceHistory_UnitPrice", "[UnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_ServicePriceHistory_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "catalog",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServicePriceHistory_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Departments_Name",
                schema: "catalog",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DepartmentId",
                schema: "catalog",
                table: "Devices",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "UX_Devices_Identifier_Active",
                schema: "catalog",
                table: "Devices",
                column: "Identifier",
                unique: true,
                filter: "[Identifier] IS NOT NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Rooms_DepartmentId",
                schema: "catalog",
                table: "Rooms",
                column: "DepartmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDevices_DeviceId_DepartmentId",
                schema: "catalog",
                table: "ServiceDevices",
                columns: new[] { "DeviceId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDevices_ServiceId_DepartmentId",
                schema: "catalog",
                table: "ServiceDevices",
                columns: new[] { "ServiceId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "UX_ServiceDevices_ServiceId_DeviceId",
                schema: "catalog",
                table: "ServiceDevices",
                columns: new[] { "ServiceId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServicePriceHistory_ChangedByUserId",
                schema: "catalog",
                table: "ServicePriceHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ServicePriceHistory_CurrentPrice",
                schema: "catalog",
                table: "ServicePriceHistory",
                column: "ServiceId",
                unique: true,
                filter: "[EffectiveTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Services_DepartmentId",
                schema: "catalog",
                table: "Services",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_SpecializationId_DepartmentId",
                schema: "catalog",
                table: "Services",
                columns: new[] { "SpecializationId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "UX_Services_SpecializationId_Name",
                schema: "catalog",
                table: "Services",
                columns: new[] { "SpecializationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Specializations_DepartmentId_Name",
                schema: "catalog",
                table: "Specializations",
                columns: new[] { "DepartmentId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rooms",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ServiceDevices",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ServicePriceHistory",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Devices",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Services",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Specializations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "catalog");
        }
    }
}
