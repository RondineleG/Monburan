using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class CarreiraInternacionalDaVaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EligibleCountries",
                table: "Jobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OffersRelocation",
                table: "Jobs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemoteScope",
                table: "Jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SponsorsVisa",
                table: "Jobs",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EligibleCountries",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "OffersRelocation",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "RemoteScope",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "SponsorsVisa",
                table: "Jobs");
        }
    }
}
