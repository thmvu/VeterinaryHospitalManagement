using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeterinaryHospitalManagement.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVeterinarianShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VeterinarianShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VeterinarianId = table.Column<int>(type: "int", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeterinarianShifts", x => x.Id);
                    table.CheckConstraint("CK_VeterinarianShifts_TimeRange", "[StartAt] < [EndAt]");
                    table.ForeignKey(
                        name: "FK_VeterinarianShifts_VeterinarianProfiles_VeterinarianId",
                        column: x => x.VeterinarianId,
                        principalTable: "VeterinarianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VeterinarianShifts_VeterinarianId_IsActive_StartAt",
                table: "VeterinarianShifts",
                columns: new[] { "VeterinarianId", "IsActive", "StartAt" })
                .Annotation("SqlServer:Include", new[] { "EndAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VeterinarianShifts");
        }
    }
}
