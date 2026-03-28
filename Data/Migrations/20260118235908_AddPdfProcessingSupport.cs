using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Isdocovac.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfProcessingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeneratedIsdocBlobName",
                table: "parsed_invoices",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneratedIsdocBlobUrl",
                table: "parsed_invoices",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LineMode",
                table: "parsed_invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpenAiCompletionTokens",
                table: "parsed_invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenAiModel",
                table: "parsed_invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenAiProcessedAt",
                table: "parsed_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpenAiPromptTokens",
                table: "parsed_invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenAiRequestJson",
                table: "parsed_invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenAiResponseJson",
                table: "parsed_invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileType",
                table: "parsed_invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TaxableSupplyDate",
                table: "parsed_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStep",
                table: "parsed_invoice_processings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StepErrorsJson",
                table: "parsed_invoice_processings",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneratedIsdocBlobName",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "GeneratedIsdocBlobUrl",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "LineMode",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "OpenAiCompletionTokens",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "OpenAiModel",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "OpenAiProcessedAt",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "OpenAiPromptTokens",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "OpenAiRequestJson",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "OpenAiResponseJson",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "SourceFileType",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "TaxableSupplyDate",
                table: "parsed_invoices");

            migrationBuilder.DropColumn(
                name: "CurrentStep",
                table: "parsed_invoice_processings");

            migrationBuilder.DropColumn(
                name: "StepErrorsJson",
                table: "parsed_invoice_processings");
        }
    }
}
