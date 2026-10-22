using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monburan.Web.Migrations
{
    /// <inheritdoc />
    public partial class IndicesDeBuscaTextual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A busca textual usava ToLower().Contains(), que o banco não consegue
            // atender por índice: um padrão com "%" na frente nunca usa índice comum, e o
            // filtro por e-mail (o mais usado do produto) comparava lower("Email") sem
            // índice que servisse. O resultado era varredura sequencial da tabela.
            //
            // pg_trgm traz o índice de trigramas, que atende busca por substring; e o
            // índice funcional sobre lower("Email") atende as consultas por e-mail, que
            // comparam exatamente.
            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS pg_trgm;""");

            // Sem e-mail repetido ignorando maiúsculas/minúsculas, o índice único sobre
            // lower("Email") pode ser criado. Se houver, a migration para com uma
            // mensagem que diz o que resolver — melhor do que um erro de índice opaco.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Candidates" GROUP BY lower("Email") HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Há candidatos com o mesmo e-mail ignorando maiúsculas/minúsculas. Corrija antes de aplicar a migration.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "Advertisers" GROUP BY lower("Email") HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Há empresas com o mesmo e-mail ignorando maiúsculas/minúsculas. Corrija antes de aplicar a migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Candidates_Email_Lower"
                    ON "Candidates" (lower("Email")) WHERE "IsDeleted" = false;
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Advertisers_Email_Lower"
                    ON "Advertisers" (lower("Email")) WHERE "IsDeleted" = false;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_Jobs_Title_Trgm"
                    ON "Jobs" USING GIN ("Title" gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_Jobs_Description_Trgm"
                    ON "Jobs" USING GIN ("Description" gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_Jobs_Category_Trgm"
                    ON "Jobs" USING GIN ("Category" gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_Candidates_Name_Trgm"
                    ON "Candidates" USING GIN ("Name" gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_Candidates_Email_Trgm"
                    ON "Candidates" USING GIN ("Email" gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_Advertisers_Name_Trgm"
                    ON "Advertisers" USING GIN ("Name" gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_Advertisers_Email_Trgm"
                    ON "Advertisers" USING GIN ("Email" gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_AdminAuditEntries_ActorEmail_Trgm"
                    ON "AdminAuditEntries" USING GIN ("ActorEmail" gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_AdminAuditEntries_TargetLabel_Trgm"
                    ON "AdminAuditEntries" USING GIN ("TargetLabel" gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_AdminAuditEntries_TargetLabel_Trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_AdminAuditEntries_ActorEmail_Trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Advertisers_Email_Trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Advertisers_Name_Trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Candidates_Email_Trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Candidates_Name_Trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Jobs_Category_Trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Jobs_Description_Trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Jobs_Title_Trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Advertisers_Email_Lower";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Candidates_Email_Lower";""");
        }
    }
}
