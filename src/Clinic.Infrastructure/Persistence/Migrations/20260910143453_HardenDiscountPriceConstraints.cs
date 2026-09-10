using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenDiscountPriceConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientPackages_Prices",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentServices_Amounts",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientPackages_Prices",
                schema: "packages",
                table: "PatientPackages",
                sql: "[BasePriceSnapshot] >= 0 AND [DiscountAmountSnapshot] >= 0 AND [NetPriceSnapshot] >= 0 AND [NetPriceSnapshot] = [BasePriceSnapshot] - [DiscountAmountSnapshot] AND ([DiscountId] IS NOT NULL OR [DiscountAmountSnapshot] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentServices_Amounts",
                schema: "appointments",
                table: "AppointmentServices",
                sql: "[UnitPrice] > 0 AND [GrossAmount] = [UnitPrice] * [Quantity] AND [DiscountAmount] >= 0 AND [PackageCoveredAmount] >= 0 AND [NetAmount] >= 0 AND [NetAmount] = [GrossAmount] - [DiscountAmount] - [PackageCoveredAmount]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientPackages_Prices",
                schema: "packages",
                table: "PatientPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentServices_Amounts",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientPackages_Prices",
                schema: "packages",
                table: "PatientPackages",
                sql: "[BasePriceSnapshot] >= 0 AND [DiscountAmountSnapshot] >= 0 AND [NetPriceSnapshot] = [BasePriceSnapshot] - [DiscountAmountSnapshot] AND ([DiscountId] IS NOT NULL OR [DiscountAmountSnapshot] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentServices_Amounts",
                schema: "appointments",
                table: "AppointmentServices",
                sql: "[UnitPrice] > 0 AND [GrossAmount] = [UnitPrice] * [Quantity] AND [DiscountAmount] >= 0 AND [PackageCoveredAmount] >= 0 AND [NetAmount] = [GrossAmount] - [DiscountAmount] - [PackageCoveredAmount]");
        }
    }
}
