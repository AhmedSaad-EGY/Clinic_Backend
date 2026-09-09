using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenPatientPackageFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientPackages_Durations",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Packages_ActivationGraceDays",
                schema: "packages",
                table: "Packages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Packages_UsageDurationDays",
                schema: "packages",
                table: "Packages");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPackageServices_PatientPackageId_SourcePackageId",
                schema: "packages",
                table: "PatientPackageServices",
                columns: new[] { "PatientPackageId", "SourcePackageId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientPackages_Durations",
                schema: "packages",
                table: "PatientPackages",
                sql: "[ActivationGraceDaysSnapshot] BETWEEN 1 AND 36500 AND [UsageDurationDaysSnapshot] BETWEEN 1 AND 36500");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Packages_ActivationGraceDays",
                schema: "packages",
                table: "Packages",
                sql: "[ActivationGraceDays] IS NULL OR [ActivationGraceDays] BETWEEN 1 AND 36500");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Packages_UsageDurationDays",
                schema: "packages",
                table: "Packages",
                sql: "[UsageDurationDays] IS NULL OR [UsageDurationDays] BETWEEN 1 AND 36500");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPackageServices_PatientPackages_PatientPackageId_SourcePackageId",
                schema: "packages",
                table: "PatientPackageServices",
                columns: new[] { "PatientPackageId", "SourcePackageId" },
                principalSchema: "packages",
                principalTable: "PatientPackages",
                principalColumns: new[] { "Id", "PackageId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientPackageServices_PatientPackages_PatientPackageId_SourcePackageId",
                schema: "packages",
                table: "PatientPackageServices");

            migrationBuilder.DropIndex(
                name: "IX_PatientPackageServices_PatientPackageId_SourcePackageId",
                schema: "packages",
                table: "PatientPackageServices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientPackages_Durations",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Packages_ActivationGraceDays",
                schema: "packages",
                table: "Packages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Packages_UsageDurationDays",
                schema: "packages",
                table: "Packages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientPackages_Durations",
                schema: "packages",
                table: "PatientPackages",
                sql: "[ActivationGraceDaysSnapshot] > 0 AND [UsageDurationDaysSnapshot] > 0");

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
        }
    }
}
