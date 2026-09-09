using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowNegativeExpectedCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Shifts_Reconciliation",
                schema: "cashier",
                table: "Shifts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shifts_Reconciliation",
                schema: "cashier",
                table: "Shifts",
                sql: "([ExpectedCash] IS NULL AND [DeclaredCash] IS NULL AND [CashVariance] IS NULL) OR ([DeclaredCash] >= 0 AND [CashVariance] = [DeclaredCash] - [ExpectedCash])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Shifts_Reconciliation",
                schema: "cashier",
                table: "Shifts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Shifts_Reconciliation",
                schema: "cashier",
                table: "Shifts",
                sql: "([ExpectedCash] IS NULL AND [DeclaredCash] IS NULL AND [CashVariance] IS NULL) OR ([ExpectedCash] >= 0 AND [DeclaredCash] >= 0 AND [CashVariance] = [DeclaredCash] - [ExpectedCash])");
        }
    }
}
