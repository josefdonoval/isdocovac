using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AzureB2CObjectId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_uploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BlobContainerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_uploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoice_uploads_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parsed_isdocs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceUploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationErrors = table.Column<string>(type: "text", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ParsedData = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parsed_isdocs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parsed_isdocs_invoice_uploads_InvoiceUploadId",
                        column: x => x.InvoiceUploadId,
                        principalTable: "invoice_uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_processings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceUploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParsedIsdocId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_processings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoice_processings_invoice_uploads_InvoiceUploadId",
                        column: x => x.InvoiceUploadId,
                        principalTable: "invoice_uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoice_processings_parsed_isdocs_ParsedIsdocId",
                        column: x => x.ParsedIsdocId,
                        principalTable: "parsed_isdocs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_processings_InvoiceUploadId",
                table: "invoice_processings",
                column: "InvoiceUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_processings_ParsedIsdocId",
                table: "invoice_processings",
                column: "ParsedIsdocId");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_processings_StartedAt",
                table: "invoice_processings",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_processings_Status",
                table: "invoice_processings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_uploads_UploadedAt",
                table: "invoice_uploads",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_uploads_UserId",
                table: "invoice_uploads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_parsed_isdocs_InvoiceUploadId",
                table: "parsed_isdocs",
                column: "InvoiceUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_parsed_isdocs_ParsedAt",
                table: "parsed_isdocs",
                column: "ParsedAt");

            migrationBuilder.CreateIndex(
                name: "IX_users_AzureB2CObjectId",
                table: "users",
                column: "AzureB2CObjectId",
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_processings");

            migrationBuilder.DropTable(
                name: "parsed_isdocs");

            migrationBuilder.DropTable(
                name: "invoice_uploads");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
