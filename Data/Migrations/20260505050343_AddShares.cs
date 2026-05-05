using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShareFifoScope",
                table: "companies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "share_quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    LastPrice = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_share_quotes_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TradeDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TradePrice = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Broker = table.Column<int>(type: "integer", nullable: false),
                    Proceeds = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Basis = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RealizedPnl = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Code = table.Column<int>(type: "integer", nullable: false),
                    CnbDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FxRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    FxAmount = table.Column<int>(type: "integer", nullable: false),
                    ProceedsCzk = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionFeeCzk = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BasisCzk = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RealizedPnlCzk = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shares_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_share_quotes_CompanyId_Symbol",
                table: "share_quotes",
                columns: new[] { "CompanyId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shares_CompanyId_Symbol",
                table: "shares",
                columns: new[] { "CompanyId", "Symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_shares_CompanyId_TradeDateTime",
                table: "shares",
                columns: new[] { "CompanyId", "TradeDateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "share_quotes");

            migrationBuilder.DropTable(
                name: "shares");

            migrationBuilder.DropColumn(
                name: "ShareFifoScope",
                table: "companies");
        }
    }
}
