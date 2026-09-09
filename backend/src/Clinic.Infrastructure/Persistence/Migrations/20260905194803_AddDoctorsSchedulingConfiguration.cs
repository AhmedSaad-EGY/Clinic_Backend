using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorsSchedulingConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Departments_Status",
                schema: "catalog",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "catalog",
                table: "Departments");

            migrationBuilder.EnsureSchema(
                name: "scheduling");

            migrationBuilder.CreateTable(
                name: "DepartmentClosures",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentClosures", x => x.Id);
                    table.CheckConstraint("CK_DepartmentClosures_TimeRange", "[StartAt] < [EndAt]");
                    table.ForeignKey(
                        name: "FK_DepartmentClosures_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "catalog",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentClosures_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentClosures_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctors", x => x.Id);
                    table.UniqueConstraint("AK_Doctors_Id_DepartmentId", x => new { x.Id, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_Doctors_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "catalog",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorExceptions",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<long>(type: "bigint", nullable: false),
                    ExceptionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorExceptions", x => x.Id);
                    table.CheckConstraint("CK_DoctorExceptions_TimeRange", "([StartTime] IS NULL AND [EndTime] IS NULL) OR ([StartTime] IS NOT NULL AND [EndTime] IS NOT NULL AND [StartTime] < [EndTime])");
                    table.CheckConstraint("CK_DoctorExceptions_Type", "[Type] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_DoctorExceptions_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalSchema: "scheduling",
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorExceptions_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorExceptions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSchedules",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSchedules", x => x.Id);
                    table.CheckConstraint("CK_DoctorSchedules_DateRange", "[EffectiveTo] IS NULL OR [EffectiveFrom] <= [EffectiveTo]");
                    table.CheckConstraint("CK_DoctorSchedules_DayOfWeek", "[DayOfWeek] BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_DoctorSchedules_TimeRange", "[StartTime] < [EndTime]");
                    table.ForeignKey(
                        name: "FK_DoctorSchedules_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalSchema: "scheduling",
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorServices",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorServices", x => x.Id);
                    table.UniqueConstraint("AK_DoctorServices_Id_ServiceId_DepartmentId", x => new { x.Id, x.ServiceId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_DoctorServices_Doctors_DoctorId_DepartmentId",
                        columns: x => new { x.DoctorId, x.DepartmentId },
                        principalSchema: "scheduling",
                        principalTable: "Doctors",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorServices_Services_ServiceId_DepartmentId",
                        columns: x => new { x.ServiceId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "Services",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentClosures_CancelledByUserId",
                schema: "scheduling",
                table: "DepartmentClosures",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentClosures_CreatedByUserId",
                schema: "scheduling",
                table: "DepartmentClosures",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentClosures_Department_TimeRange",
                schema: "scheduling",
                table: "DepartmentClosures",
                columns: new[] { "DepartmentId", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorExceptions_CancelledByUserId",
                schema: "scheduling",
                table: "DoctorExceptions",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorExceptions_CreatedByUserId",
                schema: "scheduling",
                table: "DoctorExceptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorExceptions_Doctor_Date_CancelledAt",
                schema: "scheduling",
                table: "DoctorExceptions",
                columns: new[] { "DoctorId", "ExceptionDate", "CancelledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_DepartmentId_Name",
                schema: "scheduling",
                table: "Doctors",
                columns: new[] { "DepartmentId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSchedules_Doctor_Day_Active",
                schema: "scheduling",
                table: "DoctorSchedules",
                columns: new[] { "DoctorId", "DayOfWeek", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorServices_DoctorId_DepartmentId",
                schema: "scheduling",
                table: "DoctorServices",
                columns: new[] { "DoctorId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorServices_ServiceId_DepartmentId",
                schema: "scheduling",
                table: "DoctorServices",
                columns: new[] { "ServiceId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "UX_DoctorServices_DoctorId_ServiceId",
                schema: "scheduling",
                table: "DoctorServices",
                columns: new[] { "DoctorId", "ServiceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentClosures",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "DoctorExceptions",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "DoctorSchedules",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "DoctorServices",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "Doctors",
                schema: "scheduling");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "catalog",
                table: "Departments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Departments_Status",
                schema: "catalog",
                table: "Departments",
                sql: "[Status] IN (1, 2)");
        }
    }
}
