using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BrokerImportId",
                table: "shares",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderId",
                table: "shares",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "broker_imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Broker = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BlobContainerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RowsTotal = table.Column<int>(type: "integer", nullable: false),
                    RowsImported = table.Column<int>(type: "integer", nullable: false),
                    RowsSkipped = table.Column<int>(type: "integer", nullable: false),
                    RowsDuplicate = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ParsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broker_imports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_broker_imports_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shares_BrokerImportId",
                table: "shares",
                column: "BrokerImportId");

            migrationBuilder.CreateIndex(
                name: "IX_shares_CompanyId_Broker_ExternalOrderId",
                table: "shares",
                columns: new[] { "CompanyId", "Broker", "ExternalOrderId" },
                unique: true,
                filter: "\"ExternalOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_broker_imports_CompanyId_FileHash",
                table: "broker_imports",
                columns: new[] { "CompanyId", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_broker_imports_CompanyId_UploadedAt",
                table: "broker_imports",
                columns: new[] { "CompanyId", "UploadedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_shares_broker_imports_BrokerImportId",
                table: "shares",
                column: "BrokerImportId",
                principalTable: "broker_imports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shares_broker_imports_BrokerImportId",
                table: "shares");

            migrationBuilder.DropTable(
                name: "broker_imports");

            migrationBuilder.DropIndex(
                name: "IX_shares_BrokerImportId",
                table: "shares");

            migrationBuilder.DropIndex(
                name: "IX_shares_CompanyId_Broker_ExternalOrderId",
                table: "shares");

            migrationBuilder.DropColumn(
                name: "BrokerImportId",
                table: "shares");

            migrationBuilder.DropColumn(
                name: "ExternalOrderId",
                table: "shares");
        }
    }
}
