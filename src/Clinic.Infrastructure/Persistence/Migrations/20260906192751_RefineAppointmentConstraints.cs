using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefineAppointmentConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_AppointmentServices_AppointmentId_SequenceNumber",
                schema: "appointments",
                table: "AppointmentServices",
                columns: new[] { "AppointmentId", "SequenceNumber" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AppointmentServices_Id_ServiceId",
                schema: "appointments",
                table: "AppointmentServices",
                columns: new[] { "Id", "ServiceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "AK_AppointmentServices_AppointmentId_SequenceNumber",
                schema: "appointments",
                table: "AppointmentServices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AppointmentServices_Id_ServiceId",
                schema: "appointments",
                table: "AppointmentServices");
        }
    }
}
