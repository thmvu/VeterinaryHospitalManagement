using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeterinaryHospitalManagement.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVeterinarianProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VeterinarianProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DoctorCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeterinarianProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VeterinarianProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VeterinarianProfiles_DoctorCode",
                table: "VeterinarianProfiles",
                column: "DoctorCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VeterinarianProfiles_UserId",
                table: "VeterinarianProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VeterinarianProfiles");
        }
    }
}
