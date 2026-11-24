using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class IntegracaoTokenRequestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "IntegrationTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationTokens_RequestId",
                table: "IntegrationTokens",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationTokens_RequestId",
                table: "IntegrationTokens");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "IntegrationTokens");
        }
    }
}
