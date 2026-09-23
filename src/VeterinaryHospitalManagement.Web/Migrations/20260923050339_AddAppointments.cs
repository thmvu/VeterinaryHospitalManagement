using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeterinaryHospitalManagement.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PetId = table.Column<int>(type: "int", nullable: false),
                    VeterinarianId = table.Column<int>(type: "int", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.CheckConstraint("CK_Appointments_CancellationReason", "([Status] = 'Cancelled' AND [CancellationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0) OR ([Status] <> 'Cancelled' AND [CancellationReason] IS NULL)");
                    table.CheckConstraint("CK_Appointments_Status", "[Status] IN ('Scheduled','CheckedIn','Cancelled','NoShow')");
                    table.CheckConstraint("CK_Appointments_TimeRange", "[StartAt] < [EndAt]");
                    table.ForeignKey(
                        name: "FK_Appointments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_VeterinarianProfiles_VeterinarianId",
                        column: x => x.VeterinarianId,
                        principalTable: "VeterinarianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CreatedByUserId",
                table: "Appointments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PetId_Status_StartAt",
                table: "Appointments",
                columns: new[] { "PetId", "Status", "StartAt" })
                .Annotation("SqlServer:Include", new[] { "EndAt", "VeterinarianId" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_VeterinarianId_Status_StartAt",
                table: "Appointments",
                columns: new[] { "VeterinarianId", "Status", "StartAt" })
                .Annotation("SqlServer:Include", new[] { "EndAt", "PetId" });

            migrationBuilder.CreateIndex(
                name: "UQ_Appointments_AppointmentNumber",
                table: "Appointments",
                column: "AppointmentNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments");
        }
    }
}
