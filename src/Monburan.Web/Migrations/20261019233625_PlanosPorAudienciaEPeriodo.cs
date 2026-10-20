using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <summary>
    /// Assinaturas passam a valer para candidatos também: a Subscription ganha dono candidato
    /// (exatamente um dono por linha), o plano ganha audiência, período e o flag do assistente
    /// de IA, e o candidato ganha o plano ativo. Também semeia os planos novos — os de empresa
    /// em variante anual e os de candidato (Free e Premium mensal/anual).
    /// </summary>
    public partial class PlanosPorAudienciaEPeriodo : Migration
    {
        // Id, Name, Description, Price, MaxJobs, Boost, Highlight, Analytics, MaxResumes, Audience, Period, HasAiAssistant
        private static readonly (string Id, string Name, string Description, decimal Price, int MaxJobs, bool Boost, bool Highlight, bool Analytics, int MaxResumes, string Audience, string Period, bool Ai)[] Plans =
        [
            ("80000000-0000-0000-0000-000000000004", "Premium Anual", "Plano intermediário com recursos essenciais — cobrança anual", 129.00m, 10, true, true, false, 25, "Advertiser", "Annual", false),
            ("80000000-0000-0000-0000-000000000005", "Enterprise Anual", "Plano completo para grandes empresas — cobrança anual", 499.00m, -1, true, true, true, -1, "Advertiser", "Annual", false),
            ("80000000-0000-0000-0000-000000000006", "Free", "Plano gratuito do candidato", 0m, 0, false, false, false, 3, "Candidate", "None", false),
            ("80000000-0000-0000-0000-000000000007", "Premium", "Assistente de IA para criar e melhorar o currículo", 9.90m, 0, false, false, false, -1, "Candidate", "Monthly", true),
            ("80000000-0000-0000-0000-000000000008", "Premium Anual", "Assistente de IA para criar e melhorar o currículo — cobrança anual", 19.90m, 0, false, false, false, -1, "Candidate", "Annual", true),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AdvertiserId",
                table: "Subscriptions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CandidateId",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "SubscriptionPlans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Advertiser");

            migrationBuilder.AddColumn<bool>(
                name: "HasAiAssistant",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Period",
                table: "SubscriptionPlans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<string>(
                name: "ActivePlan",
                table: "Candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_CandidateId",
                table: "Subscriptions",
                column: "CandidateId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscriptions_Owner",
                table: "Subscriptions",
                sql: "(\"AdvertiserId\" IS NOT NULL) <> (\"CandidateId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Candidates_CandidateId",
                table: "Subscriptions",
                column: "CandidateId",
                principalTable: "Candidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Plano gratuito não tem vigência: o Free de empresa veio com o default Monthly
            migrationBuilder.Sql(
                """
                UPDATE "SubscriptionPlans" SET "Period" = 'None' WHERE "Price" = 0;
                """);

            // Perfis sem plano registrado ficam no Free, como acontece com as contas novas
            migrationBuilder.Sql(
                """
                UPDATE "Advertisers" SET "ActivePlan" = 'Free' WHERE "ActivePlan" IS NULL;
                UPDATE "Candidates" SET "ActivePlan" = 'Free' WHERE "ActivePlan" IS NULL;
                """);

            foreach (var plan in Plans)
            {
                migrationBuilder.Sql(
                    $"""
                    INSERT INTO "SubscriptionPlans" ("Id", "Name", "Description", "Price", "MaxJobs", "HasBoost", "HasHighlight", "HasAnalytics", "MaxResumes", "IsActive", "Audience", "Period", "HasAiAssistant", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "TenantId", "UpdatedAt", "UpdatedBy")
                    SELECT '{plan.Id}', '{plan.Name}', '{plan.Description}', {plan.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {plan.MaxJobs}, {Bool(plan.Boost)}, {Bool(plan.Highlight)}, {Bool(plan.Analytics)}, {plan.MaxResumes}, TRUE, '{plan.Audience}', '{plan.Period}', {Bool(plan.Ai)}, now(), 'system', NULL, '', FALSE, 'default', NULL, ''
                    WHERE NOT EXISTS (SELECT 1 FROM "SubscriptionPlans" WHERE "Name" = '{plan.Name}' AND "Audience" = '{plan.Audience}' AND "Period" = '{plan.Period}')
                    ON CONFLICT ("Id") DO NOTHING;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Candidates_CandidateId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_CandidateId",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscriptions_Owner",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CandidateId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasAiAssistant",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "Period",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "ActivePlan",
                table: "Candidates");

            migrationBuilder.AlterColumn<Guid>(
                name: "AdvertiserId",
                table: "Subscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Remove só os planos criados aqui e ainda sem assinaturas
            foreach (var plan in Plans)
            {
                migrationBuilder.Sql(
                    $"""
                    DELETE FROM "SubscriptionPlans" AS p
                    WHERE p."Id" = '{plan.Id}'
                      AND NOT EXISTS (SELECT 1 FROM "Subscriptions" AS s WHERE s."PlanId" = p."Id");
                    """);
            }
        }

        private static string Bool(bool value) => value ? "TRUE" : "FALSE";
    }
}
