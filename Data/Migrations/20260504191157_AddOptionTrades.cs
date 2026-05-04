using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredCurrency",
                table: "companies",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "CZK");

            migrationBuilder.CreateTable(
                name: "option_trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TradeDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TradePrice = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Multiplier = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_option_trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_option_trades_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_option_trades_CompanyId_Symbol",
                table: "option_trades",
                columns: new[] { "CompanyId", "Symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_option_trades_CompanyId_TradeDateTime",
                table: "option_trades",
                columns: new[] { "CompanyId", "TradeDateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "option_trades");

            migrationBuilder.DropColumn(
                name: "PreferredCurrency",
                table: "companies");
        }
    }
}
