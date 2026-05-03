using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMultiCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authentication_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authentication_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fx_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fx_rates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "login_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WasSuccessful = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_attempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    EmailVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsLegalEntity = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Titul = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Ico = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Street = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    HouseNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OrientNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    City = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Zip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaxOfficeCode = table.Column<int>(type: "integer", nullable: true),
                    TaxOfficeBranchCode = table.Column<int>(type: "integer", nullable: true),
                    OkecCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DataBoxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VatPeriod = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_companies_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionTokenHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Titul = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsLegalEntity = table.Column<bool>(type: "boolean", nullable: false),
                    Dic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Ico = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Street = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    HouseNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OrientNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    City = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Zip = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaxOfficeCode = table.Column<int>(type: "integer", nullable: true),
                    TaxOfficeBranchCode = table.Column<int>(type: "integer", nullable: true),
                    OkecCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DataBoxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VatPeriod = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contacts_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fakturoid_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
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
                        name: "FK_fakturoid_connections_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fakturoid_oauth_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fakturoid_oauth_states", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fakturoid_oauth_states_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
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
                    TaxableSupplyDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    FakturoidUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedToInvoiceAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsImported = table.Column<bool>(type: "boolean", nullable: false)
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
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    TaxableSupplyDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoices_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_fakturoid_invoices_FakturoidInvoiceId",
                        column: x => x.FakturoidInvoiceId,
                        principalTable: "fakturoid_invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "parsed_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    SourceFileType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LineMode = table.Column<int>(type: "integer", nullable: true),
                    GeneratedIsdocBlobName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    GeneratedIsdocBlobUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    OpenAiModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OpenAiPromptTokens = table.Column<int>(type: "integer", nullable: true),
                    OpenAiCompletionTokens = table.Column<int>(type: "integer", nullable: true),
                    OpenAiRequestJson = table.Column<string>(type: "jsonb", nullable: true),
                    OpenAiResponseJson = table.Column<string>(type: "jsonb", nullable: true),
                    OpenAiProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TaxableSupplyDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParsedDataJson = table.Column<string>(type: "jsonb", nullable: true),
                    LinesJson = table.Column<string>(type: "jsonb", nullable: true),
                    VatRatesSummary = table.Column<string>(type: "jsonb", nullable: true),
                    ImportedToInvoiceAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parsed_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parsed_invoices_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parsed_invoices_invoices_ImportedInvoiceId",
                        column: x => x.ImportedInvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id");
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
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    CurrentStep = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StepErrorsJson = table.Column<string>(type: "jsonb", nullable: true)
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
                name: "IX_authentication_tokens_Email_ExpiresAt",
                table: "authentication_tokens",
                columns: new[] { "Email", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_authentication_tokens_ExpiresAt",
                table: "authentication_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_authentication_tokens_TokenHash",
                table: "authentication_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_OwnerUserId",
                table: "companies",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_companies_OwnerUserId_IsActive",
                table: "companies",
                columns: new[] { "OwnerUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_CompanyId",
                table: "contacts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_CompanyId_Kind",
                table: "contacts",
                columns: new[] { "CompanyId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_connections_AccountSlug",
                table: "fakturoid_connections",
                column: "AccountSlug");

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_connections_CompanyId",
                table: "fakturoid_connections",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_connections_CompanyId_IsActive",
                table: "fakturoid_connections",
                columns: new[] { "CompanyId", "IsActive" });

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

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_oauth_states_CompanyId_StateHash",
                table: "fakturoid_oauth_states",
                columns: new[] { "CompanyId", "StateHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fakturoid_oauth_states_ExpiresAt",
                table: "fakturoid_oauth_states",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_fx_rates_Date_Currency",
                table: "fx_rates",
                columns: new[] { "Date", "Currency" },
                unique: true);

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
                name: "IX_invoices_CompanyId",
                table: "invoices",
                column: "CompanyId");

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
                name: "IX_login_attempts_Email_AttemptedAt",
                table: "login_attempts",
                columns: new[] { "Email", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_login_attempts_IpAddress_AttemptedAt",
                table: "login_attempts",
                columns: new[] { "IpAddress", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_parsed_invoice_processings_ParsedInvoiceId",
                table: "parsed_invoice_processings",
                column: "ParsedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_parsed_invoices_CompanyId",
                table: "parsed_invoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_parsed_invoices_ImportedInvoiceId",
                table: "parsed_invoices",
                column: "ImportedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_ExpiresAt",
                table: "user_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_SessionTokenHash",
                table: "user_sessions",
                column: "SessionTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId_IsActive",
                table: "user_sessions",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);

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
                name: "FK_companies_users_OwnerUserId",
                table: "companies");

            migrationBuilder.DropForeignKey(
                name: "FK_fakturoid_connections_companies_CompanyId",
                table: "fakturoid_connections");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_companies_CompanyId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_parsed_invoices_companies_CompanyId",
                table: "parsed_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_fakturoid_invoices_FakturoidInvoiceId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_parsed_invoices_invoices_ImportedInvoiceId",
                table: "parsed_invoices");

            migrationBuilder.DropTable(
                name: "authentication_tokens");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropTable(
                name: "fakturoid_invoice_attachments");

            migrationBuilder.DropTable(
                name: "fakturoid_invoice_lines");

            migrationBuilder.DropTable(
                name: "fakturoid_invoice_payments");

            migrationBuilder.DropTable(
                name: "fakturoid_oauth_states");

            migrationBuilder.DropTable(
                name: "fx_rates");

            migrationBuilder.DropTable(
                name: "invoice_attachments");

            migrationBuilder.DropTable(
                name: "invoice_lines");

            migrationBuilder.DropTable(
                name: "invoice_payments");

            migrationBuilder.DropTable(
                name: "login_attempts");

            migrationBuilder.DropTable(
                name: "parsed_invoice_processings");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "fakturoid_invoices");

            migrationBuilder.DropTable(
                name: "fakturoid_connections");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "parsed_invoices");
        }
    }
}
