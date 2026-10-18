using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAiProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Protocol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ApiKeyEnvVar = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    SendTemperature = table.Column<bool>(type: "boolean", nullable: false),
                    UseJsonResponseFormat = table.Column<bool>(type: "boolean", nullable: false),
                    UseMaxCompletionTokens = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviders_IsDefault",
                table: "AiProviders",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AiProviders_Name",
                table: "AiProviders",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AiProviders_TenantId_Id",
                table: "AiProviders",
                columns: new[] { "TenantId", "Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProviders");
        }
    }
}
