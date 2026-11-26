using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class DeduplicacaoAlertaVaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JobAlertId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Notifications_JobAlertId_JobId",
                table: "Notifications",
                columns: new[] { "JobAlertId", "JobId" },
                unique: true,
                filter: "\"JobAlertId\" IS NOT NULL AND \"JobId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Notifications_JobAlertId_JobId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "JobAlertId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "Notifications");
        }
    }
}
