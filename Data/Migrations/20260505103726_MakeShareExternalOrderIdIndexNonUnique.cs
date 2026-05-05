using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeShareExternalOrderIdIndexNonUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shares_CompanyId_Broker_ExternalOrderId",
                table: "shares");

            migrationBuilder.CreateIndex(
                name: "IX_shares_CompanyId_Broker_ExternalOrderId",
                table: "shares",
                columns: new[] { "CompanyId", "Broker", "ExternalOrderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shares_CompanyId_Broker_ExternalOrderId",
                table: "shares");

            migrationBuilder.CreateIndex(
                name: "IX_shares_CompanyId_Broker_ExternalOrderId",
                table: "shares",
                columns: new[] { "CompanyId", "Broker", "ExternalOrderId" },
                unique: true,
                filter: "\"ExternalOrderId\" IS NOT NULL");
        }
    }
}
