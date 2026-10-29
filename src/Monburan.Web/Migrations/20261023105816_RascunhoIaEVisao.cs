using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class RascunhoIaEVisao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AiDraftAt",
                table: "JobSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiDraftEvidenceHash",
                table: "JobSubmissions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiDraftJson",
                table: "JobSubmissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiDraftProvider",
                table: "JobSubmissions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsVision",
                table: "AiProviders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiDraftAt",
                table: "JobSubmissions");

            migrationBuilder.DropColumn(
                name: "AiDraftEvidenceHash",
                table: "JobSubmissions");

            migrationBuilder.DropColumn(
                name: "AiDraftJson",
                table: "JobSubmissions");

            migrationBuilder.DropColumn(
                name: "AiDraftProvider",
                table: "JobSubmissions");

            migrationBuilder.DropColumn(
                name: "SupportsVision",
                table: "AiProviders");
        }
    }
}
