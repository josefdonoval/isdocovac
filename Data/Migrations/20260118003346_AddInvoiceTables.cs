using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportedInvoiceId",
                table: "fakturoid_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedToInvoiceAt",
                table: "fakturoid_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsImported",
                table: "fakturoid_invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "invoice_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    BlobContainerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BlobName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    BlobUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    UnitPriceWithoutVat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitPriceWithVat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPriceWithoutVat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalVat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalPriceWithVat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VariableSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    FakturoidInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParsedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Open = table.Column<bool>(type: "boolean", nullable: false),
                    Sent = table.Column<bool>(type: "boolean", nullable: false),
                    Overdue = table.Column<bool>(type: "boolean", nullable: false),
                    Paid = table.Column<bool>(type: "boolean", nullable: false),
                    Cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    IssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Due = table.Column<int>(type: "integer", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    VatPriceMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClientStreet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClientCity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ClientZip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClientCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientRegistrationNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientVatNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientHasDeliveryAddress = table.Column<bool>(type: "boolean", nullable: false),
                    ClientDeliveryName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClientDeliveryStreet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClientDeliveryCity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ClientDeliveryZip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClientDeliveryCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupplierStreet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupplierCity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SupplierZip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SupplierCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierRegistrationNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierVatNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VariableSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConstantSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SpecificSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankAccount = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Iban = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SwiftBic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    FooterNote = table.Column<string>(type: "text", nullable: true),
                    PrivateNote = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: true),
                    VatRatesSummary = table.Column<string>(type: "jsonb", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoices_fakturoid_invoices_FakturoidInvoiceId",
                        column: x => x.FakturoidInvoiceId,
                        principalTable: "fakturoid_invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_invoices_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parsed_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BlobContainerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ParsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationErrors = table.Column<string>(type: "text", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    VatPriceMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupplierStreet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupplierCity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SupplierZip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SupplierCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierRegistrationNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierVatNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomerStreet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomerCity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CustomerZip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CustomerCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerRegistrationNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerVatNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VariableSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConstantSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SpecificSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankAccount = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Iban = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    ParsedDataJson = table.Column<string>(type: "jsonb", nullable: true),
                    LinesJson = table.Column<string>(type: "jsonb", nullable: true),
                    VatRatesSummary = table.Column<string>(type: "jsonb", nullable: true),
                    ImportedToInvoiceAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parsed_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parsed_invoices_invoices_ImportedInvoiceId",
                        column: x => x.ImportedInvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_parsed_invoices_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parsed_invoice_processings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParsedInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parsed_invoice_processings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parsed_invoice_processings_parsed_invoices_ParsedInvoiceId",
                        column: x => x.ParsedInvoiceId,
                        principalTable: "parsed_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_attachments_InvoiceId",
                table: "invoice_attachments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_InvoiceId",
                table: "invoice_lines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_InvoiceId",
                table: "invoice_payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_FakturoidInvoiceId",
                table: "invoices",
                column: "FakturoidInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_ParsedInvoiceId",
                table: "invoices",
                column: "ParsedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_UserId",
                table: "invoices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_parsed_invoice_processings_ParsedInvoiceId",
                table: "parsed_invoice_processings",
                column: "ParsedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_parsed_invoices_ImportedInvoiceId",
                table: "parsed_invoices",
                column: "ImportedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_parsed_invoices_UserId",
                table: "parsed_invoices",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_attachments_invoices_InvoiceId",
                table: "invoice_attachments",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_lines_invoices_InvoiceId",
                table: "invoice_lines",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_payments_invoices_InvoiceId",
                table: "invoice_payments",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_parsed_invoices_ParsedInvoiceId",
                table: "invoices",
                column: "ParsedInvoiceId",
                principalTable: "parsed_invoices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_parsed_invoices_invoices_ImportedInvoiceId",
                table: "parsed_invoices");

            migrationBuilder.DropTable(
                name: "invoice_attachments");

            migrationBuilder.DropTable(
                name: "invoice_lines");

            migrationBuilder.DropTable(
                name: "invoice_payments");

            migrationBuilder.DropTable(
                name: "parsed_invoice_processings");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "ImportedInvoiceId",
                table: "fakturoid_invoices");

            migrationBuilder.DropColumn(
                name: "ImportedToInvoiceAt",
                table: "fakturoid_invoices");

            migrationBuilder.DropColumn(
                name: "IsImported",
                table: "fakturoid_invoices");
        }
    }
}
