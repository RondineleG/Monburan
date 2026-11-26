using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class HistoricoVersaoDonoCandidatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorUserId",
                table: "ApplicationStatusHistory",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ApplicationStatusHistory",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Applications",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE "ApplicationStatusHistory" AS history
                SET "Version" = numbered.version
                FROM (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "ApplicationId"
                               ORDER BY "Date", "CreatedAt", "Id"
                           ) AS version
                    FROM "ApplicationStatusHistory"
                ) AS numbered
                WHERE history."Id" = numbered."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "UX_ApplicationStatusHistory_ApplicationId_Version",
                table: "ApplicationStatusHistory",
                columns: new[] { "ApplicationId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ApplicationStatusHistory_ApplicationId_Version",
                table: "ApplicationStatusHistory");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "ApplicationStatusHistory");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ApplicationStatusHistory");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Applications");
        }
    }
}
