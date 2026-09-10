using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowOptionalDiscountOverrideReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentServices_Discount",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentServices_Discount",
                schema: "appointments",
                table: "AppointmentServices",
                sql: "([DiscountId] IS NOT NULL OR [DiscountAmount] = 0) AND ([DiscountOverrideMode] IS NULL AND [DiscountOverrideByAdminUserId] IS NULL AND [DiscountOverrideReason] IS NULL OR [DiscountOverrideMode] = 1 AND [DiscountId] IS NOT NULL AND [DiscountOverrideByAdminUserId] IS NOT NULL AND ([DiscountOverrideReason] IS NULL OR LEN([DiscountOverrideReason]) BETWEEN 1 AND 500) OR [DiscountOverrideMode] = 2 AND [DiscountId] IS NULL AND [DiscountAmount] = 0 AND [DiscountOverrideByAdminUserId] IS NOT NULL AND ([DiscountOverrideReason] IS NULL OR LEN([DiscountOverrideReason]) BETWEEN 1 AND 500))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentServices_Discount",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentServices_Discount",
                schema: "appointments",
                table: "AppointmentServices",
                sql: "([DiscountId] IS NOT NULL OR [DiscountAmount] = 0) AND ([DiscountOverrideMode] IS NULL AND [DiscountOverrideByAdminUserId] IS NULL AND [DiscountOverrideReason] IS NULL OR [DiscountOverrideMode] = 1 AND [DiscountId] IS NOT NULL AND [DiscountOverrideByAdminUserId] IS NOT NULL AND LEN([DiscountOverrideReason]) BETWEEN 1 AND 500 OR [DiscountOverrideMode] = 2 AND [DiscountId] IS NULL AND [DiscountAmount] = 0 AND [DiscountOverrideByAdminUserId] IS NOT NULL AND LEN([DiscountOverrideReason]) BETWEEN 1 AND 500)");
        }
    }
}
