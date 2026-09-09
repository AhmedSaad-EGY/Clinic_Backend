using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "appointments");

            migrationBuilder.CreateTable(
                name: "Appointments",
                schema: "appointments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<long>(type: "bigint", nullable: false),
                    RoomId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CancelledByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.UniqueConstraint("AK_Appointments_Id_DepartmentId", x => new { x.Id, x.DepartmentId });
                    table.UniqueConstraint("AK_Appointments_Id_PatientId", x => new { x.Id, x.PatientId });
                    table.CheckConstraint("CK_Appointments_Amounts", "[SubtotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [NetAmount] >= 0 AND [NetAmount] = [SubtotalAmount] - [DiscountAmount]");
                    table.CheckConstraint("CK_Appointments_PaymentStatus", "[PaymentStatus] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Appointments_Status", "[Status] IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_Appointments_TimeRange", "[StartAt] < [EndAt]");
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "patients",
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Rooms_RoomId_DepartmentId",
                        columns: x => new { x.RoomId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "Rooms",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentServices",
                schema: "appointments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    DoctorServiceId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    SegmentStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    SegmentEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentServices", x => x.Id);
                    table.UniqueConstraint("AK_AppointmentServices_Id_ServiceId_DepartmentId", x => new { x.Id, x.ServiceId, x.DepartmentId });
                    table.CheckConstraint("CK_AppointmentServices_Amounts", "[UnitPrice] > 0 AND [GrossAmount] = [UnitPrice] * [Quantity] AND [DiscountAmount] >= 0 AND [NetAmount] = [GrossAmount] - [DiscountAmount]");
                    table.CheckConstraint("CK_AppointmentServices_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_AppointmentServices_Status", "[Status] IN (1, 2, 3)");
                    table.CheckConstraint("CK_AppointmentServices_TimeRange", "[SegmentStartAt] < [SegmentEndAt]");
                    table.ForeignKey(
                        name: "FK_AppointmentServices_Appointments_AppointmentId_DepartmentId",
                        columns: x => new { x.AppointmentId, x.DepartmentId },
                        principalSchema: "appointments",
                        principalTable: "Appointments",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentServices_DoctorServices_DoctorServiceId_ServiceId_DepartmentId",
                        columns: x => new { x.DoctorServiceId, x.ServiceId, x.DepartmentId },
                        principalSchema: "scheduling",
                        principalTable: "DoctorServices",
                        principalColumns: new[] { "Id", "ServiceId", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentServices_Services_ServiceId_DepartmentId",
                        columns: x => new { x.ServiceId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "Services",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentDevices",
                schema: "appointments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceDeviceId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceId = table.Column<long>(type: "bigint", nullable: false),
                    ReservedFrom = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ReservedTo = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentDevices", x => x.Id);
                    table.CheckConstraint("CK_AppointmentDevices_TimeRange", "[ReservedFrom] < [ReservedTo]");
                    table.ForeignKey(
                        name: "FK_AppointmentDevices_AppointmentServices_AppointmentServiceId_ServiceId_DepartmentId",
                        columns: x => new { x.AppointmentServiceId, x.ServiceId, x.DepartmentId },
                        principalSchema: "appointments",
                        principalTable: "AppointmentServices",
                        principalColumns: new[] { "Id", "ServiceId", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentDevices_Devices_DeviceId_DepartmentId",
                        columns: x => new { x.DeviceId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "Devices",
                        principalColumns: new[] { "Id", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentDevices_ServiceDevices_ServiceDeviceId_ServiceId_DepartmentId",
                        columns: x => new { x.ServiceDeviceId, x.ServiceId, x.DepartmentId },
                        principalSchema: "catalog",
                        principalTable: "ServiceDevices",
                        principalColumns: new[] { "Id", "ServiceId", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDevices_AppointmentServiceId_ServiceId_DepartmentId",
                schema: "appointments",
                table: "AppointmentDevices",
                columns: new[] { "AppointmentServiceId", "ServiceId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDevices_Device_TimeRange",
                schema: "appointments",
                table: "AppointmentDevices",
                columns: new[] { "DeviceId", "ReservedFrom", "ReservedTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDevices_DeviceId_DepartmentId",
                schema: "appointments",
                table: "AppointmentDevices",
                columns: new[] { "DeviceId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDevices_ServiceDeviceId_ServiceId_DepartmentId",
                schema: "appointments",
                table: "AppointmentDevices",
                columns: new[] { "ServiceDeviceId", "ServiceId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "UX_AppointmentDevices_Service_Device",
                schema: "appointments",
                table: "AppointmentDevices",
                columns: new[] { "AppointmentServiceId", "ServiceDeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CancelledByUserId",
                schema: "appointments",
                table: "Appointments",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CreatedByUserId",
                schema: "appointments",
                table: "Appointments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Department_TimeRange",
                schema: "appointments",
                table: "Appointments",
                columns: new[] { "DepartmentId", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Patient_StartAt",
                schema: "appointments",
                table: "Appointments",
                columns: new[] { "PatientId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_RoomId_DepartmentId",
                schema: "appointments",
                table: "Appointments",
                columns: new[] { "RoomId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status_StartAt",
                schema: "appointments",
                table: "Appointments",
                columns: new[] { "Status", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_UpdatedByUserId",
                schema: "appointments",
                table: "Appointments",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_Appointment_Sequence",
                schema: "appointments",
                table: "AppointmentServices",
                columns: new[] { "AppointmentId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_AppointmentId_DepartmentId",
                schema: "appointments",
                table: "AppointmentServices",
                columns: new[] { "AppointmentId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_Doctor_TimeRange",
                schema: "appointments",
                table: "AppointmentServices",
                columns: new[] { "DoctorServiceId", "SegmentStartAt", "SegmentEndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_DoctorServiceId_ServiceId_DepartmentId",
                schema: "appointments",
                table: "AppointmentServices",
                columns: new[] { "DoctorServiceId", "ServiceId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_ServiceId_DepartmentId",
                schema: "appointments",
                table: "AppointmentServices",
                columns: new[] { "ServiceId", "DepartmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentDevices",
                schema: "appointments");

            migrationBuilder.DropTable(
                name: "AppointmentServices",
                schema: "appointments");

            migrationBuilder.DropTable(
                name: "Appointments",
                schema: "appointments");
        }
    }
}
