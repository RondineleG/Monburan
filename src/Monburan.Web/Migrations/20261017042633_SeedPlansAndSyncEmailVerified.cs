using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <summary>
    /// 1) Planos de assinatura padrão: até aqui só o seed de Development os criava, então em
    ///    produção a página de planos ficava vazia e não havia como assinar. Idempotente —
    ///    não duplica planos que já existam com o mesmo nome.
    /// 2) Sincroniza IsEmailVerified dos perfis com o EmailConfirmed da conta (o flag nunca
    ///    era atualizado na confirmação de e-mail).
    /// </summary>
    public partial class SeedPlansAndSyncEmailVerified : Migration
    {
        private static readonly (string Id, string Name, string Description, decimal Price, int MaxJobs, bool Boost, bool Highlight, bool Analytics, int MaxResumes)[] Plans =
        [
            ("80000000-0000-0000-0000-000000000001", "Free", "Plano básico gratuito para pequenas empresas", 0m, 5, false, false, false, 5),
            ("80000000-0000-0000-0000-000000000002", "Premium", "Plano intermediário com recursos essenciais", 12.90m, 10, true, true, false, 25),
            ("80000000-0000-0000-0000-000000000003", "Enterprise", "Plano completo para grandes empresas", 49.90m, -1, true, true, true, -1),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var plan in Plans)
            {
                migrationBuilder.Sql(
                    $"""
                    INSERT INTO "SubscriptionPlans" ("Id", "Name", "Description", "Price", "MaxJobs", "HasBoost", "HasHighlight", "HasAnalytics", "MaxResumes", "IsActive", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "TenantId", "UpdatedAt", "UpdatedBy")
                    SELECT '{plan.Id}', '{plan.Name}', '{plan.Description}', {plan.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {plan.MaxJobs}, {Bool(plan.Boost)}, {Bool(plan.Highlight)}, {Bool(plan.Analytics)}, {plan.MaxResumes}, TRUE, now(), 'system', NULL, '', FALSE, 'default', NULL, ''
                    WHERE NOT EXISTS (SELECT 1 FROM "SubscriptionPlans" WHERE "Name" = '{plan.Name}')
                    ON CONFLICT ("Id") DO NOTHING;
                    """);
            }

            migrationBuilder.Sql(
                """
                UPDATE "Candidates" AS c SET "IsEmailVerified" = TRUE
                FROM "Identity"."Users" AS u
                WHERE u."EmailConfirmed" AND u."NormalizedEmail" = UPPER(c."Email") AND NOT c."IsEmailVerified";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Advertisers" AS a SET "IsEmailVerified" = TRUE
                FROM "Identity"."Users" AS u
                WHERE u."EmailConfirmed" AND u."NormalizedEmail" = UPPER(a."Email") AND NOT a."IsEmailVerified";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove só os planos criados aqui e ainda sem assinaturas; o flag de e-mail
            // verificado reflete um fato e não é revertido.
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
