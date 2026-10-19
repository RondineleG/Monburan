using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateJourney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractType",
                table: "Jobs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryPeriod",
                table: "Jobs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CareerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContractType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DesiredRole = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MinimumSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
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
                    table.PrimaryKey("PK_CareerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CareerProfiles_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Keywords = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastMatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SalaryMin = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
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
                    table.PrimaryKey("PK_JobAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobAlerts_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_SavedJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedJobs_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedJobs_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareerProfiles_CandidateId",
                table: "CareerProfiles",
                column: "CandidateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareerProfiles_TenantId_Id",
                table: "CareerProfiles",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobAlerts_CandidateId",
                table: "JobAlerts",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_JobAlerts_IsActive",
                table: "JobAlerts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_JobAlerts_TenantId_Id",
                table: "JobAlerts",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedJobs_CandidateId_JobId",
                table: "SavedJobs",
                columns: new[] { "CandidateId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedJobs_CreatedAt",
                table: "SavedJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SavedJobs_JobId",
                table: "SavedJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedJobs_TenantId_Id",
                table: "SavedJobs",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            // Backfill: uma linha de histórico por candidatura existente, com o status atual e a data da candidatura
            migrationBuilder.Sql("""
                INSERT INTO "ApplicationStatusHistory"
                    ("Id", "ApplicationId", "Status", "Date", "Note", "Responsible", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "TenantId", "UpdatedAt", "UpdatedBy")
                SELECT gen_random_uuid(), a."Id", a."Status", a."CreatedAt", NULL, NULL, a."CreatedAt", a."CreatedBy", NULL, '', false, a."TenantId", NULL, ''
                FROM "Applications" a
                WHERE NOT EXISTS (
                    SELECT 1 FROM "ApplicationStatusHistory" h WHERE h."ApplicationId" = a."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareerProfiles");

            migrationBuilder.DropTable(
                name: "JobAlerts");

            migrationBuilder.DropTable(
                name: "SavedJobs");

            migrationBuilder.DropColumn(
                name: "ContractType",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "SalaryPeriod",
                table: "Jobs");
        }
    }
}
