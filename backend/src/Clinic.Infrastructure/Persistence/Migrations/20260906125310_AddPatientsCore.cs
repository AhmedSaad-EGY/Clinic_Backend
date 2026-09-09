using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "patients");

            migrationBuilder.CreateSequence(
                name: "PatientFileNumberSequence",
                schema: "patients");

            migrationBuilder.CreateTable(
                name: "Patients",
                schema: "patients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileNumber = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [patients].[PatientFileNumberSequence]"),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrimaryPhoneNumber = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    SecondaryPhoneNumber = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AgeAtRegistration = table.Column<int>(type: "int", nullable: true),
                    AgeRecordedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GuardianName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GuardianPhoneNumber = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.CheckConstraint("CK_Patients_AgeRange", "[AgeAtRegistration] IS NULL OR [AgeAtRegistration] BETWEEN 0 AND 130");
                    table.CheckConstraint("CK_Patients_AgeSource", "([BirthDate] IS NOT NULL AND [AgeAtRegistration] IS NULL AND [AgeRecordedAt] IS NULL) OR ([BirthDate] IS NULL AND [AgeAtRegistration] IS NOT NULL AND [AgeRecordedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_Patients_DifferentPhones", "[SecondaryPhoneNumber] IS NULL OR [SecondaryPhoneNumber] <> [PrimaryPhoneNumber]");
                    table.CheckConstraint("CK_Patients_Gender", "[Gender] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Patients_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Patients_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientNotes",
                schema: "patients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    NoteText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientNotes", x => x.Id);
                    table.CheckConstraint("CK_PatientNotes_Visibility", "[Visibility] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_PatientNotes_Patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "patients",
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentHistory",
                schema: "patients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentHistory_Patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "patients",
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentHistory_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientNotes_CreatedByUserId",
                schema: "patients",
                table: "PatientNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientNotes_Patient_Visibility_Archived",
                schema: "patients",
                table: "PatientNotes",
                columns: new[] { "PatientId", "Visibility", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Area_Gender",
                schema: "patients",
                table: "Patients",
                columns: new[] { "Area", "Gender" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_CreatedByUserId",
                schema: "patients",
                table: "Patients",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_FullName",
                schema: "patients",
                table: "Patients",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UpdatedByUserId",
                schema: "patients",
                table: "Patients",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Patients_FileNumber",
                schema: "patients",
                table: "Patients",
                column: "FileNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Patients_PrimaryPhoneNumber",
                schema: "patients",
                table: "Patients",
                column: "PrimaryPhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentHistory_CreatedByUserId",
                schema: "patients",
                table: "TreatmentHistory",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentHistory_Patient_Date_Archived",
                schema: "patients",
                table: "TreatmentHistory",
                columns: new[] { "PatientId", "EventDate", "IsArchived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientNotes",
                schema: "patients");

            migrationBuilder.DropTable(
                name: "TreatmentHistory",
                schema: "patients");

            migrationBuilder.DropTable(
                name: "Patients",
                schema: "patients");

            migrationBuilder.DropSequence(
                name: "PatientFileNumberSequence",
                schema: "patients");
        }
    }
}
