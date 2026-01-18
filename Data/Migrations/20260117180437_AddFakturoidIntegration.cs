using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFakturoidIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fakturoid_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AccountSlug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fakturoid_connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fakturoid_connections_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fakturoid_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FakturoidConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FakturoidId = table.Column<int>(type: "integer", nullable: false),
                    CustomId = table.Column<string>(type: "text", nullable: true),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Open = table.Column<bool>(type: "boolean", nullable: false),
                    Sent = table.Column<bool>(type: "boolean", nullable: false),
                    Overdue = table.Column<bool>(type: "boolean", nullable: false),
                    Paid = table.Column<bool>(type: "boolean", nullable: false),
                    Cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    Uncollectible = table.Column<bool>(type: "boolean", nullable: false),
                    IssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UncollectibleAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    NativeSubtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    NativeTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    NativeRemainingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    VatPriceMode = table.Column<string>(type: "text", nullable: true),
                    Due = table.Column<int>(type: "integer", nullable: true),
                    SubjectId = table.Column<int>(type: "integer", nullable: true),
                    SubjectCustomId = table.Column<string>(type: "text", nullable: true),
                    ClientName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClientStreet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClientCity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ClientZip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClientCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientRegistrationNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientVatNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientLocalVatNo = table.Column<string>(type: "text", nullable: true),
                    ClientHasDeliveryAddress = table.Column<bool>(type: "boolean", nullable: false),
                    ClientDeliveryName = table.Column<string>(type: "text", nullable: true),
                    ClientDeliveryStreet = table.Column<string>(type: "text", nullable: true),
                    ClientDeliveryCity = table.Column<string>(type: "text", nullable: true),
                    ClientDeliveryZip = table.Column<string>(type: "text", nullable: true),
                    ClientDeliveryCountry = table.Column<string>(type: "text", nullable: true),
                    YourName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    YourStreet = table.Column<string>(type: "text", nullable: true),
                    YourCity = table.Column<string>(type: "text", nullable: true),
                    YourZip = table.Column<string>(type: "text", nullable: true),
                    YourCountry = table.Column<string>(type: "text", nullable: true),
                    YourRegistrationNo = table.Column<string>(type: "text", nullable: true),
                    YourVatNo = table.Column<string>(type: "text", nullable: true),
                    YourLocalVatNo = table.Column<string>(type: "text", nullable: true),
                    VariableSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConstantSymbol = table.Column<string>(type: "text", nullable: true),
                    SpecificSymbol = table.Column<string>(type: "text", nullable: true),
                    NumberFormatId = table.Column<int>(type: "integer", nullable: true),
                    GeneratorId = table.Column<int>(type: "integer", nullable: true),
                    RelatedId = table.Column<int>(type: "integer", nullable: true),
                    CorrectionId = table.Column<int>(type: "integer", nullable: true),
                    ProformaFollowupDocument = table.Column<string>(type: "text", nullable: true),
                    Paypal = table.Column<string>(type: "text", nullable: true),
                    Gopay = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    FooterNote = table.Column<string>(type: "text", nullable: true),
                    PrivateNote = table.Column<string>(type: "text", nullable: true),
                    BankAccountId = table.Column<int>(type: "integer", nullable: true),
                    BankAccount = table.Column<string>(type: "text", nullable: true),
                    Iban = table.Column<string>(type: "text", nullable: true),
                    SwiftBic = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: true),
                    VatRatesSummary = table.Column<string>(type: "jsonb", nullable: true),
                    NativeVatRatesSummary = table.Column<string>(type: "jsonb", nullable: true),
                    PaidAdvances = table.Column<string>(type: "jsonb", nullable: true),
                    HtmlUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PublicHtmlUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FakturoidUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fakturoid_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fakturoid_invoices_fakturoid_connections_FakturoidConnectio~",
                        column: x => x.FakturoidConnectionId,
                        principalTable: "fakturoid_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fakturoid_invoice_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FakturoidInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DownloadUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fakturoid_invoice_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fakturoid_invoice_attachments_fakturoid_invoices_FakturoidI~",
                        column: x => x.FakturoidInvoiceId,
                        principalTable: "fakturoid_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fakturoid_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FakturoidInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    InventoryItemId = table.Column<int>(type: "integer", nullable: true),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fakturoid_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fakturoid_invoice_lines_fakturoid_invoices_FakturoidInvoice~",
                        column: x => x.FakturoidInvoiceId,
                        principalTable: "fakturoid_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fakturoid_invoice_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FakturoidInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FakturoidPaymentId = table.Column<int>(type: "integer", nullable: false),
                    PaidOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NativeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    VariableSymbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankAccountId = table.Column<int>(type: "integer", nullable: true),
                    TaxDocumentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fakturoid_invoice_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fakturoid_invoice_payments_fakturoid_invoices_FakturoidInvo~",
                        column: x => x.FakturoidInvoiceId,
                        principalTable: "fakturoid_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_connections_AccountSlug",
                table: "fakturoid_connections",
                column: "AccountSlug");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_connections_UserId",
                table: "fakturoid_connections",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_connections_UserId_IsActive",
                table: "fakturoid_connections",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoice_attachments_FakturoidInvoiceId",
                table: "fakturoid_invoice_attachments",
                column: "FakturoidInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoice_lines_FakturoidInvoiceId",
                table: "fakturoid_invoice_lines",
                column: "FakturoidInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoice_lines_FakturoidInvoiceId_LineOrder",
                table: "fakturoid_invoice_lines",
                columns: new[] { "FakturoidInvoiceId", "LineOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoice_payments_FakturoidInvoiceId",
                table: "fakturoid_invoice_payments",
                column: "FakturoidInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoice_payments_FakturoidInvoiceId_FakturoidPaym~",
                table: "fakturoid_invoice_payments",
                columns: new[] { "FakturoidInvoiceId", "FakturoidPaymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoice_payments_FakturoidPaymentId",
                table: "fakturoid_invoice_payments",
                column: "FakturoidPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoice_payments_PaidOn",
                table: "fakturoid_invoice_payments",
                column: "PaidOn");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoices_DueOn",
                table: "fakturoid_invoices",
                column: "DueOn");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoices_FakturoidConnectionId",
                table: "fakturoid_invoices",
                column: "FakturoidConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoices_FakturoidConnectionId_FakturoidId",
                table: "fakturoid_invoices",
                columns: new[] { "FakturoidConnectionId", "FakturoidId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoices_FakturoidConnectionId_LastSyncedAt",
                table: "fakturoid_invoices",
                columns: new[] { "FakturoidConnectionId", "LastSyncedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoices_FakturoidId",
                table: "fakturoid_invoices",
                column: "FakturoidId");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoices_IssuedOn",
                table: "fakturoid_invoices",
                column: "IssuedOn");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoices_Status",
                table: "fakturoid_invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_invoices_UpdatedAt",
                table: "fakturoid_invoices",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fakturoid_invoice_attachments");

            migrationBuilder.DropTable(
                name: "fakturoid_invoice_lines");

            migrationBuilder.DropTable(
                name: "fakturoid_invoice_payments");

            migrationBuilder.DropTable(
                name: "fakturoid_invoices");

            migrationBuilder.DropTable(
                name: "fakturoid_connections");
        }
    }
}
