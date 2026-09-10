using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenPackageSessionBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PackageSessionBookings_AppointmentServices_AppointmentServiceId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings");

            migrationBuilder.DropIndex(
                name: "IX_PackageSessionBookings_AppointmentServiceId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings");

            migrationBuilder.DropIndex(
                name: "UX_PackageSessionBookings_ReservedSession",
                schema: "packages",
                table: "PackageSessionBookings");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AppointmentServices_Id_AppointmentId_ServiceId",
                schema: "appointments",
                table: "AppointmentServices",
                columns: new[] { "Id", "AppointmentId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageSessionBookings_AppointmentServiceId_AppointmentId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings",
                columns: new[] { "AppointmentServiceId", "AppointmentId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PackageSessionBookings_ReservedSession",
                schema: "packages",
                table: "PackageSessionBookings",
                column: "PackageSessionId",
                unique: true,
                filter: "[Status] <> 2");

            migrationBuilder.AddForeignKey(
                name: "FK_PackageSessionBookings_AppointmentServices_AppointmentServiceId_AppointmentId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings",
                columns: new[] { "AppointmentServiceId", "AppointmentId", "ServiceId" },
                principalSchema: "appointments",
                principalTable: "AppointmentServices",
                principalColumns: new[] { "Id", "AppointmentId", "ServiceId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [packages].[TR_PackageSessionBookings_ValidateOwnership]
                ON [packages].[PackageSessionBookings]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted AS booking
                        INNER JOIN [appointments].[Appointments] AS appointment
                            ON appointment.[Id] = booking.[AppointmentId]
                        INNER JOIN [appointments].[AppointmentServices] AS appointmentService
                            ON appointmentService.[Id] = booking.[AppointmentServiceId]
                        WHERE appointment.[PatientPackageId] IS NULL
                           OR appointment.[PatientPackageId] <> booking.[PatientPackageId]
                           OR appointmentService.[AppointmentId] <> booking.[AppointmentId]
                           OR appointmentService.[ServiceId] <> booking.[ServiceId])
                        THROW 51001, 'Package session booking ownership mismatch.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS [packages].[TR_PackageSessionBookings_ValidateOwnership];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PackageSessionBookings_AppointmentServices_AppointmentServiceId_AppointmentId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings");

            migrationBuilder.DropIndex(
                name: "IX_PackageSessionBookings_AppointmentServiceId_AppointmentId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings");

            migrationBuilder.DropIndex(
                name: "UX_PackageSessionBookings_ReservedSession",
                schema: "packages",
                table: "PackageSessionBookings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AppointmentServices_Id_AppointmentId_ServiceId",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.CreateIndex(
                name: "IX_PackageSessionBookings_AppointmentServiceId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings",
                columns: new[] { "AppointmentServiceId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PackageSessionBookings_ReservedSession",
                schema: "packages",
                table: "PackageSessionBookings",
                column: "PackageSessionId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_PackageSessionBookings_AppointmentServices_AppointmentServiceId_ServiceId",
                schema: "packages",
                table: "PackageSessionBookings",
                columns: new[] { "AppointmentServiceId", "ServiceId" },
                principalSchema: "appointments",
                principalTable: "AppointmentServices",
                principalColumns: new[] { "Id", "ServiceId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
