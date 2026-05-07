using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceInternalNumberAndOriginalDocumentNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Number",
                table: "invoices",
                newName: "InternalNumber");

            migrationBuilder.AddColumn<string>(
                name: "OriginalDocumentNumber",
                table: "invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CompanyId_InternalNumber",
                table: "invoices",
                columns: new[] { "CompanyId", "InternalNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CompanyId_OriginalDocumentNumber",
                table: "invoices",
                columns: new[] { "CompanyId", "OriginalDocumentNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_CompanyId_InternalNumber",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_CompanyId_OriginalDocumentNumber",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "OriginalDocumentNumber",
                table: "invoices");

            migrationBuilder.RenameColumn(
                name: "InternalNumber",
                table: "invoices",
                newName: "Number");
        }
    }
}
