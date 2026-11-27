using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class RecruivaMigrationBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MigrationBatchId",
                schema: "Identity",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_MigrationBatchId",
                schema: "Identity",
                table: "Users",
                column: "MigrationBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_MigrationBatchId",
                schema: "Identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MigrationBatchId",
                schema: "Identity",
                table: "Users");
        }
    }
}
