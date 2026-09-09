using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenClinicalRecordsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_DepartmentId",
                schema: "clinical",
                table: "FollowUps",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Departments_DepartmentId",
                schema: "clinical",
                table: "FollowUps",
                column: "DepartmentId",
                principalSchema: "catalog",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Patients_PatientId",
                schema: "clinical",
                table: "FollowUps",
                column: "PatientId",
                principalSchema: "patients",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Departments_DepartmentId",
                schema: "clinical",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Patients_PatientId",
                schema: "clinical",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_DepartmentId",
                schema: "clinical",
                table: "FollowUps");
        }
    }
}
