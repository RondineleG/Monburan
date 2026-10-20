using Microsoft.EntityFrameworkCore;

namespace Monburan.Web.Repositories;

/// <summary>
/// Busca textual no banco.
/// <para>
/// No PostgreSQL a busca usa <c>ILIKE</c>, que aproveita o índice de trigramas (pg_trgm).
/// Com <c>ToLower().Contains()</c> o banco fazia varredura sequencial da tabela inteira:
/// o índice comum não serve para um padrão com "%" na frente, e o filtro de e-mail
/// (o mais usado do produto) comparava <c>lower("Email")</c> sem índice que servisse.
/// No provider InMemory — usado pelos testes de API — o <c>ILIKE</c> não existe, então a
/// consulta cai para <c>ToLower().Contains()</c>.
/// </para>
/// </summary>
internal static class TextSearch
{
    private const string InMemoryProvider = "Microsoft.EntityFrameworkCore.InMemory";

    /// <summary>O provider em uso sabe traduzir <c>ILIKE</c>?</summary>
    public static bool SupportsILike(DbContext context) =>
        context.Database.ProviderName != InMemoryProvider;

    /// <summary>
    /// Padrão de busca. O "%" na frente é justamente o que exige o índice de trigramas —
    /// sem ele a busca por substring não usa índice nenhum.
    /// </summary>
    public static string Pattern(string term) => $"%{term}%";
}
