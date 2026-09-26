using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeterinaryHospitalManagement.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "InvoiceNumberSequence",
                minValue: 1L);

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    OwnerNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OwnerPhoneSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PetNameSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ProcessedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ProcessedByNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.CheckConstraint("CK_Invoices_PaymentMethod", "[PaymentMethod] IN ('Cash', 'BankTransfer')");
                    table.CheckConstraint("CK_Invoices_TotalAmount", "[TotalAmount] >= 0 AND [TotalAmount] = FLOOR([TotalAmount])");
                    table.ForeignKey(
                        name: "FK_Invoices_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    VisitServiceId = table.Column<int>(type: "int", nullable: false),
                    DescriptionSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceItems", x => x.Id);
                    table.CheckConstraint("CK_InvoiceItems_LineTotal", "[LineTotal] >= 0 AND [LineTotal] = FLOOR([LineTotal])");
                    table.CheckConstraint("CK_InvoiceItems_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InvoiceItems_UnitPrice", "[UnitPrice] >= 0 AND [UnitPrice] = FLOOR([UnitPrice])");
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceItems_VisitServices_VisitServiceId",
                        column: x => x.VisitServiceId,
                        principalTable: "VisitServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_VisitServiceId",
                table: "InvoiceItems",
                column: "VisitServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PaidAt",
                table: "Invoices",
                column: "PaidAt");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ProcessedByUserId",
                table: "Invoices",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_VisitId",
                table: "Invoices",
                column: "VisitId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceItems");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropSequence(
                name: "InvoiceNumberSequence");
        }
    }
}
