using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeterinaryHospitalManagement.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnersAndPets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "OwnerCodeSequence",
                minValue: 1L,
                maxValue: 999999L);

            migrationBuilder.CreateSequence(
                name: "PetCodeSequence",
                minValue: 1L,
                maxValue: 999999L);

            migrationBuilder.CreateTable(
                name: "Owners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Owners", x => x.Id);
                    table.CheckConstraint("CK_Owners_OwnerCode_Format", "DATALENGTH([OwnerCode]) = 20 AND [OwnerCode] COLLATE Latin1_General_100_BIN2 LIKE N'OWN-[0-9][0-9][0-9][0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_Owners_PhoneNumber_Canonical", "DATALENGTH([PhoneNumber]) = 24 AND [PhoneNumber] COLLATE Latin1_General_100_BIN2 LIKE N'+84[35789][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
                });

            migrationBuilder.CreateTable(
                name: "Species",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Species", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Breeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpeciesId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Breeds", x => x.Id);
                    table.UniqueConstraint("AK_Breeds_Id_SpeciesId", x => new { x.Id, x.SpeciesId });
                    table.ForeignKey(
                        name: "FK_Breeds_Species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "Species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PetCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SpeciesId = table.Column<int>(type: "int", nullable: false),
                    BreedId = table.Column<int>(type: "int", nullable: true),
                    Sex = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pets", x => x.Id);
                    table.CheckConstraint("CK_Pets_PetCode_Format", "DATALENGTH([PetCode]) = 20 AND [PetCode] COLLATE Latin1_General_100_BIN2 LIKE N'PET-[0-9][0-9][0-9][0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_Pets_Sex", "[Sex] IN ('Unknown', 'Male', 'Female')");
                    table.ForeignKey(
                        name: "FK_Pets_Breeds_BreedId_SpeciesId",
                        columns: x => new { x.BreedId, x.SpeciesId },
                        principalTable: "Breeds",
                        principalColumns: new[] { "Id", "SpeciesId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pets_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pets_Species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "Species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Breeds_SpeciesId_Name",
                table: "Breeds",
                columns: new[] { "SpeciesId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Owners_OwnerCode",
                table: "Owners",
                column: "OwnerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Owners_PhoneNumber",
                table: "Owners",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Pets_BreedId_SpeciesId",
                table: "Pets",
                columns: new[] { "BreedId", "SpeciesId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pets_OwnerId",
                table: "Pets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Pets_PetCode",
                table: "Pets",
                column: "PetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pets_SpeciesId",
                table: "Pets",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_Species_Code",
                table: "Species",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pets");

            migrationBuilder.DropTable(
                name: "Breeds");

            migrationBuilder.DropTable(
                name: "Owners");

            migrationBuilder.DropTable(
                name: "Species");

            migrationBuilder.DropSequence(
                name: "OwnerCodeSequence");

            migrationBuilder.DropSequence(
                name: "PetCodeSequence");
        }
    }
}
