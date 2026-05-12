using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalOriginInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mailbox_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ImapHost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ImapPort = table.Column<int>(type: "integer", nullable: false),
                    UseSsl = table.Column<bool>(type: "boolean", nullable: false),
                    Username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordEncrypted = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ProcessedFolderName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    LastSeenUid = table.Column<long>(type: "bigint", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastPolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mailbox_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mailbox_accounts_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_ingestion_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MailboxAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImapUid = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: false),
                    From = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RawHeadersJson = table.Column<string>(type: "jsonb", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_ingestion_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_ingestion_messages_mailbox_accounts_MailboxAccountId",
                        column: x => x.MailboxAccountId,
                        principalTable: "mailbox_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_origin_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    BlobContainerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BlobName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Oversized = table.Column<bool>(type: "boolean", nullable: false),
                    IsInvoiceCandidate = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ParsedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailIngestionMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_origin_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_external_origin_files_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_external_origin_files_email_ingestion_messages_EmailIngesti~",
                        column: x => x.EmailIngestionMessageId,
                        principalTable: "email_ingestion_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_external_origin_files_parsed_invoices_ParsedInvoiceId",
                        column: x => x.ParsedInvoiceId,
                        principalTable: "parsed_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_ingestion_messages_CompanyId",
                table: "email_ingestion_messages",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_email_ingestion_messages_MailboxAccountId",
                table: "email_ingestion_messages",
                column: "MailboxAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_email_ingestion_messages_MailboxAccountId_MessageId",
                table: "email_ingestion_messages",
                columns: new[] { "MailboxAccountId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_origin_files_CompanyId",
                table: "external_origin_files",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_external_origin_files_CompanyId_Status_CreatedAt",
                table: "external_origin_files",
                columns: new[] { "CompanyId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_external_origin_files_EmailIngestionMessageId",
                table: "external_origin_files",
                column: "EmailIngestionMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_external_origin_files_ParsedInvoiceId",
                table: "external_origin_files",
                column: "ParsedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_mailbox_accounts_CompanyId",
                table: "mailbox_accounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_mailbox_accounts_CompanyId_Enabled",
                table: "mailbox_accounts",
                columns: new[] { "CompanyId", "Enabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_origin_files");

            migrationBuilder.DropTable(
                name: "email_ingestion_messages");

            migrationBuilder.DropTable(
                name: "mailbox_accounts");
        }
    }
}
