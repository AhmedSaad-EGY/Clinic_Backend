using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalRecordsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clinical");

            migrationBuilder.CreateTable(
                name: "FollowUps",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionId = table.Column<long>(type: "bigint", nullable: false),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResultAppointmentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ClosedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowUps", x => x.Id);
                    table.CheckConstraint("CK_FollowUps_Result", "([Status] = 1 AND [ResultAppointmentId] IS NULL AND [ClosedAt] IS NULL) OR ([Status] = 2 AND [ResultAppointmentId] IS NOT NULL AND [ClosedAt] IS NOT NULL) OR ([Status] = 3 AND [ResultAppointmentId] IS NULL AND [ClosedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_FollowUps_Status", "[Status] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_FollowUps_Appointments_ResultAppointmentId_PatientId",
                        columns: x => new { x.ResultAppointmentId, x.PatientId },
                        principalSchema: "appointments",
                        principalTable: "Appointments",
                        principalColumns: new[] { "Id", "PatientId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FollowUps_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FollowUps_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItems",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionRevisionId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    MedicineName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DoseAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    DoseUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TimesPerDay = table.Column<int>(type: "int", nullable: true),
                    FrequencyText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DurationText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FoodTiming = table.Column<int>(type: "int", nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItems", x => x.Id);
                    table.CheckConstraint("CK_PrescriptionItems_Dose", "[DoseAmount] IS NULL OR [DoseAmount] > 0");
                    table.CheckConstraint("CK_PrescriptionItems_FoodTiming", "[FoodTiming] IS NULL OR [FoodTiming] IN (1, 2)");
                    table.CheckConstraint("CK_PrescriptionItems_Order", "[SortOrder] > 0");
                    table.CheckConstraint("CK_PrescriptionItems_Times", "[TimesPerDay] IS NULL OR [TimesPerDay] > 0");
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionRevisions",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionId = table.Column<long>(type: "bigint", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionRevisions", x => x.Id);
                    table.UniqueConstraint("AK_PrescriptionRevisions_Id_PrescriptionId", x => new { x.Id, x.PrescriptionId });
                    table.CheckConstraint("CK_PrescriptionRevisions_Number", "[RevisionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_PrescriptionRevisions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Prescriptions",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentServiceId = table.Column<long>(type: "bigint", nullable: false),
                    CurrentRevisionId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    FinalizedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    VoidedByAdminUserId = table.Column<long>(type: "bigint", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.Id);
                    table.UniqueConstraint("AK_Prescriptions_Id_AppointmentServiceId", x => new { x.Id, x.AppointmentServiceId });
                    table.CheckConstraint("CK_Prescriptions_Status", "[Status] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Prescriptions_AppointmentServices_AppointmentServiceId",
                        column: x => x.AppointmentServiceId,
                        principalSchema: "appointments",
                        principalTable: "AppointmentServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_PrescriptionRevisions_CurrentRevisionId_Id",
                        columns: x => new { x.CurrentRevisionId, x.Id },
                        principalSchema: "clinical",
                        principalTable: "PrescriptionRevisions",
                        principalColumns: new[] { "Id", "PrescriptionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Users_FinalizedByUserId",
                        column: x => x.FinalizedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Users_VoidedByAdminUserId",
                        column: x => x.VoidedByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_ClosedByUserId",
                schema: "clinical",
                table: "FollowUps",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_CreatedByUserId",
                schema: "clinical",
                table: "FollowUps",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_PatientId_ReturnDate",
                schema: "clinical",
                table: "FollowUps",
                columns: new[] { "PatientId", "ReturnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_PrescriptionId",
                schema: "clinical",
                table: "FollowUps",
                column: "PrescriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_ResultAppointmentId_PatientId",
                schema: "clinical",
                table: "FollowUps",
                columns: new[] { "ResultAppointmentId", "PatientId" },
                unique: true,
                filter: "[ResultAppointmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_Status_ReturnDate",
                schema: "clinical",
                table: "FollowUps",
                columns: new[] { "Status", "ReturnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_PrescriptionRevisionId_SortOrder",
                schema: "clinical",
                table: "PrescriptionItems",
                columns: new[] { "PrescriptionRevisionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionRevisions_CreatedByUserId",
                schema: "clinical",
                table: "PrescriptionRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionRevisions_PrescriptionId_RevisionNumber",
                schema: "clinical",
                table: "PrescriptionRevisions",
                columns: new[] { "PrescriptionId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_AppointmentServiceId",
                schema: "clinical",
                table: "Prescriptions",
                column: "AppointmentServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_CreatedByUserId",
                schema: "clinical",
                table: "Prescriptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_CurrentRevisionId_Id",
                schema: "clinical",
                table: "Prescriptions",
                columns: new[] { "CurrentRevisionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_FinalizedByUserId",
                schema: "clinical",
                table: "Prescriptions",
                column: "FinalizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_VoidedByAdminUserId",
                schema: "clinical",
                table: "Prescriptions",
                column: "VoidedByAdminUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Prescriptions_PrescriptionId",
                schema: "clinical",
                table: "FollowUps",
                column: "PrescriptionId",
                principalSchema: "clinical",
                principalTable: "Prescriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrescriptionItems_PrescriptionRevisions_PrescriptionRevisionId",
                schema: "clinical",
                table: "PrescriptionItems",
                column: "PrescriptionRevisionId",
                principalSchema: "clinical",
                principalTable: "PrescriptionRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrescriptionRevisions_Prescriptions_PrescriptionId",
                schema: "clinical",
                table: "PrescriptionRevisions",
                column: "PrescriptionId",
                principalSchema: "clinical",
                principalTable: "Prescriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrescriptionRevisions_Prescriptions_PrescriptionId",
                schema: "clinical",
                table: "PrescriptionRevisions");

            migrationBuilder.DropTable(
                name: "FollowUps",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "PrescriptionItems",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "Prescriptions",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "PrescriptionRevisions",
                schema: "clinical");
        }
    }
}
