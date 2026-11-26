using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class IntegracaoHiMarshall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    Scopes = table.Column<string>(type: "character varying(200)", unicode: false, maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_IntegrationTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationTokens_CandidateId",
                table: "IntegrationTokens",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationTokens_TenantId_Id",
                table: "IntegrationTokens",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationTokens_UserId_RevokedAt",
                table: "IntegrationTokens",
                columns: new[] { "UserId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_IntegrationTokens_TokenHash",
                table: "IntegrationTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationTokens");
        }
    }
}
