using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePatientPhoneUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT [PhoneNumber]
                    FROM
                    (
                        SELECT [PrimaryPhoneNumber] AS [PhoneNumber]
                        FROM [patients].[Patients]
                        UNION ALL
                        SELECT [SecondaryPhoneNumber]
                        FROM [patients].[Patients]
                        WHERE [SecondaryPhoneNumber] IS NOT NULL
                    ) AS [PatientPhones]
                    GROUP BY [PhoneNumber]
                    HAVING COUNT_BIG(*) > 1
                )
                    THROW 51001,
                        'Patient phone uniqueness migration requires duplicate phone cleanup.',
                        1;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Patients_SecondaryPhoneNumber",
                schema: "patients",
                table: "Patients",
                column: "SecondaryPhoneNumber",
                unique: true,
                filter: "[SecondaryPhoneNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Patients_SecondaryPhoneNumber",
                schema: "patients",
                table: "Patients");
        }
    }
}
