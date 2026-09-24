using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeterinaryHospitalManagement.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "VisitNumberSequence",
                minValue: 1L,
                maxValue: 9999L);

            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AppointmentId = table.Column<int>(type: "int", nullable: true),
                    PetId = table.Column<int>(type: "int", nullable: false),
                    VeterinarianId = table.Column<int>(type: "int", nullable: false),
                    PetNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OwnerNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OwnerPhoneSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VeterinarianNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CheckedInAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CheckedInByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.CheckConstraint("CK_Visits_Cancelled", "NOT(Status = 'Cancelled' AND (CancellationReason IS NULL OR StartedAt IS NOT NULL OR CompletedAt IS NOT NULL))");
                    table.CheckConstraint("CK_Visits_Completed", "NOT(Status = 'Completed' AND (StartedAt IS NULL OR CompletedAt IS NULL OR CancellationReason IS NOT NULL))");
                    table.CheckConstraint("CK_Visits_Completed_Order", "NOT(Status = 'Completed' AND CompletedAt IS NOT NULL AND StartedAt IS NOT NULL AND CompletedAt <= StartedAt)");
                    table.CheckConstraint("CK_Visits_InProgress", "NOT(Status = 'InProgress' AND (StartedAt IS NULL OR CompletedAt IS NOT NULL OR CancellationReason IS NOT NULL))");
                    table.CheckConstraint("CK_Visits_Waiting", "NOT(Status = 'Waiting' AND (StartedAt IS NOT NULL OR CompletedAt IS NOT NULL OR CancellationReason IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_Visits_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_AspNetUsers_CheckedInByUserId",
                        column: x => x.CheckedInByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_VeterinarianProfiles_VeterinarianId",
                        column: x => x.VeterinarianId,
                        principalTable: "VeterinarianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_AppointmentId",
                table: "Visits",
                column: "AppointmentId",
                unique: true,
                filter: "[AppointmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_CheckedInByUserId",
                table: "Visits",
                column: "CheckedInByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_Pet_ActiveStatus",
                table: "Visits",
                column: "PetId",
                unique: true,
                filter: "[Status] IN ('Waiting', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_Vet_InProgress",
                table: "Visits",
                column: "VeterinarianId",
                unique: true,
                filter: "[Status] = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_VisitNumber",
                table: "Visits",
                column: "VisitNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Visits");

            migrationBuilder.DropSequence(
                name: "VisitNumberSequence");
        }
    }
}
