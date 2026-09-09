using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cashier");

            migrationBuilder.CreateTable(
                name: "CashDrawers",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SecretaryUserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDrawers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashDrawers_Users_SecretaryUserId",
                        column: x => x.SecretaryUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftPolicies",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ClosingGraceMinutes = table.Column<int>(type: "int", nullable: false),
                    UpdatedByAdminUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftPolicies", x => x.Id);
                    table.CheckConstraint("CK_ShiftPolicies_ClosingGraceMinutes", "[ClosingGraceMinutes] BETWEEN 0 AND 120");
                    table.CheckConstraint("CK_ShiftPolicies_Singleton", "[Id] = 1");
                    table.ForeignKey(
                        name: "FK_ShiftPolicies_Users_UpdatedByAdminUserId",
                        column: x => x.UpdatedByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                schema: "cashier",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashDrawerId = table.Column<long>(type: "bigint", nullable: false),
                    ScheduledStart = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ScheduledEnd = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    GraceEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ActualOpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OpeningBalanceEnteredByUserId = table.Column<long>(type: "bigint", nullable: true),
                    OpeningBalanceEnteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DeclaredCash = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CashVariance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpenedAutomatically = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledByAdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    ClosedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CloseNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CancelledByAdminUserId = table.Column<long>(type: "bigint", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.CheckConstraint("CK_Shifts_OpeningBalance", "([OpeningBalance] IS NULL AND [OpeningBalanceEnteredByUserId] IS NULL AND [OpeningBalanceEnteredAt] IS NULL) OR ([OpeningBalance] >= 0 AND [OpeningBalanceEnteredByUserId] IS NOT NULL AND [OpeningBalanceEnteredAt] IS NOT NULL)");
                    table.CheckConstraint("CK_Shifts_Reconciliation", "([ExpectedCash] IS NULL AND [DeclaredCash] IS NULL AND [CashVariance] IS NULL) OR ([ExpectedCash] >= 0 AND [DeclaredCash] >= 0 AND [CashVariance] = [DeclaredCash] - [ExpectedCash])");
                    table.CheckConstraint("CK_Shifts_Status", "[Status] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Shifts_TerminalState", "([Status] = 1 AND [ActualOpenedAt] IS NULL AND [OpeningBalance] IS NULL AND [ExpectedCash] IS NULL AND [ClosedAt] IS NULL AND [ClosedByUserId] IS NULL AND [CancelledAt] IS NULL AND [CancelledByAdminUserId] IS NULL AND [CancellationReason] IS NULL) OR ([Status] BETWEEN 2 AND 3 AND [ActualOpenedAt] IS NOT NULL AND [ClosedAt] IS NULL AND [ClosedByUserId] IS NULL AND [CancelledAt] IS NULL AND [CancelledByAdminUserId] IS NULL AND [CancellationReason] IS NULL) OR ([Status] = 4 AND [ActualOpenedAt] IS NOT NULL AND [OpeningBalance] IS NOT NULL AND [ExpectedCash] IS NOT NULL AND [DeclaredCash] IS NOT NULL AND [CashVariance] IS NOT NULL AND [ClosedAt] IS NOT NULL AND [ClosedByUserId] IS NOT NULL AND [CancelledAt] IS NULL AND [CancelledByAdminUserId] IS NULL AND [CancellationReason] IS NULL) OR ([Status] = 5 AND [ActualOpenedAt] IS NULL AND [OpeningBalance] IS NULL AND [ExpectedCash] IS NULL AND [CancelledAt] IS NOT NULL AND [CancelledByAdminUserId] IS NOT NULL AND [CancellationReason] IS NOT NULL AND [ClosedAt] IS NULL AND [ClosedByUserId] IS NULL)");
                    table.CheckConstraint("CK_Shifts_TimeRange", "[ScheduledStart] < [ScheduledEnd] AND [ScheduledEnd] <= [GraceEndsAt]");
                    table.ForeignKey(
                        name: "FK_Shifts_CashDrawers_CashDrawerId",
                        column: x => x.CashDrawerId,
                        principalSchema: "cashier",
                        principalTable: "CashDrawers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shifts_Users_CancelledByAdminUserId",
                        column: x => x.CancelledByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shifts_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shifts_Users_OpeningBalanceEnteredByUserId",
                        column: x => x.OpeningBalanceEnteredByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shifts_Users_ScheduledByAdminUserId",
                        column: x => x.ScheduledByAdminUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "cashier",
                table: "ShiftPolicies",
                columns: new[] { "Id", "ClosingGraceMinutes", "UpdatedAt", "UpdatedByAdminUserId" },
                values: new object[] { 1L, 10, null, null });

            migrationBuilder.CreateIndex(
                name: "UX_CashDrawers_SecretaryUserId",
                schema: "cashier",
                table: "CashDrawers",
                column: "SecretaryUserId",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO [cashier].[CashDrawers]
                    ([SecretaryUserId], [Name], [IsActive], [CreatedAt])
                SELECT
                    [user].[Id],
                    CONCAT(N'درج - ', [user].[FullName]),
                    CAST(1 AS bit),
                    SYSDATETIMEOFFSET() AT TIME ZONE 'UTC'
                FROM [identity].[Users] AS [user]
                INNER JOIN [identity].[UserRoles] AS [userRole]
                    ON [user].[Id] = [userRole].[UserId]
                INNER JOIN [identity].[Roles] AS [role]
                    ON [userRole].[RoleId] = [role].[Id]
                WHERE [role].[NormalizedName] = N'SECRETARY'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [cashier].[CashDrawers] AS [drawer]
                      WHERE [drawer].[SecretaryUserId] = [user].[Id]);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftPolicies_UpdatedByAdminUserId",
                schema: "cashier",
                table: "ShiftPolicies",
                column: "UpdatedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_CancelledByAdminUserId",
                schema: "cashier",
                table: "Shifts",
                column: "CancelledByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ClosedByUserId",
                schema: "cashier",
                table: "Shifts",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_Drawer_TimeRange",
                schema: "cashier",
                table: "Shifts",
                columns: new[] { "CashDrawerId", "ScheduledStart", "GraceEndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_OpeningBalanceEnteredByUserId",
                schema: "cashier",
                table: "Shifts",
                column: "OpeningBalanceEnteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ScheduledByAdminUserId",
                schema: "cashier",
                table: "Shifts",
                column: "ScheduledByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_Status_Start",
                schema: "cashier",
                table: "Shifts",
                columns: new[] { "Status", "ScheduledStart" });

            migrationBuilder.CreateIndex(
                name: "UX_Shifts_Drawer_Start_Active",
                schema: "cashier",
                table: "Shifts",
                columns: new[] { "CashDrawerId", "ScheduledStart" },
                unique: true,
                filter: "[Status] <> 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftPolicies",
                schema: "cashier");

            migrationBuilder.DropTable(
                name: "Shifts",
                schema: "cashier");

            migrationBuilder.DropTable(
                name: "CashDrawers",
                schema: "cashier");
        }
    }
}
