using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenAppointmentsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentDevices_ServiceDevices_ServiceDeviceId_ServiceId_DepartmentId",
                schema: "appointments",
                table: "AppointmentDevices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentServices_Status",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentDevices_ServiceDeviceId_ServiceId_DepartmentId",
                schema: "appointments",
                table: "AppointmentDevices");

            migrationBuilder.AddColumn<int>(
                name: "StatusBeforeSuspension",
                schema: "appointments",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [appointments].[Appointments]
                SET [StatusBeforeSuspension] = 1
                WHERE [Status] = 6 AND [StatusBeforeSuspension] IS NULL;
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ServiceDevices_Id_ServiceId_DepartmentId_DeviceId",
                schema: "catalog",
                table: "ServiceDevices",
                columns: new[] { "Id", "ServiceId", "DepartmentId", "DeviceId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentServices_Status",
                schema: "appointments",
                table: "AppointmentServices",
                sql: "[Status] IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_StatusBeforeSuspension",
                schema: "appointments",
                table: "Appointments",
                sql: "([Status] = 6 AND [StatusBeforeSuspension] IN (1, 2)) OR ([Status] <> 6 AND [StatusBeforeSuspension] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDevices_ServiceDeviceId_ServiceId_DepartmentId_DeviceId",
                schema: "appointments",
                table: "AppointmentDevices",
                columns: new[] { "ServiceDeviceId", "ServiceId", "DepartmentId", "DeviceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentDevices_ServiceDevices_ServiceDeviceId_ServiceId_DepartmentId_DeviceId",
                schema: "appointments",
                table: "AppointmentDevices",
                columns: new[] { "ServiceDeviceId", "ServiceId", "DepartmentId", "DeviceId" },
                principalSchema: "catalog",
                principalTable: "ServiceDevices",
                principalColumns: new[] { "Id", "ServiceId", "DepartmentId", "DeviceId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentDevices_ServiceDevices_ServiceDeviceId_ServiceId_DepartmentId_DeviceId",
                schema: "appointments",
                table: "AppointmentDevices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ServiceDevices_Id_ServiceId_DepartmentId_DeviceId",
                schema: "catalog",
                table: "ServiceDevices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AppointmentServices_Status",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_StatusBeforeSuspension",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentDevices_ServiceDeviceId_ServiceId_DepartmentId_DeviceId",
                schema: "appointments",
                table: "AppointmentDevices");

            migrationBuilder.DropColumn(
                name: "StatusBeforeSuspension",
                schema: "appointments",
                table: "Appointments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AppointmentServices_Status",
                schema: "appointments",
                table: "AppointmentServices",
                sql: "[Status] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDevices_ServiceDeviceId_ServiceId_DepartmentId",
                schema: "appointments",
                table: "AppointmentDevices",
                columns: new[] { "ServiceDeviceId", "ServiceId", "DepartmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentDevices_ServiceDevices_ServiceDeviceId_ServiceId_DepartmentId",
                schema: "appointments",
                table: "AppointmentDevices",
                columns: new[] { "ServiceDeviceId", "ServiceId", "DepartmentId" },
                principalSchema: "catalog",
                principalTable: "ServiceDevices",
                principalColumns: new[] { "Id", "ServiceId", "DepartmentId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
