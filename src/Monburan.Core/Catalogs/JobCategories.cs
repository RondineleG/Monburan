namespace Monburan.Core.Catalogs;

/// <summary>
/// Categorias oferecidas na interface. A API continua aceitando texto livre: vagas e telas antigas guardam valores
/// fora desta lista e não podem deixar de ser lidas.
/// </summary>
public static class JobCategories
{
    public static IReadOnlyList<string> All { get; } =
    [
        "Administrativo",
        "Atendimento ao cliente",
        "Comercial e vendas",
        "Contabilidade e finanças",
        "Design e criação",
        "Educação",
        "Engenharia e manufatura",
        "Indústria e produção",
        "Jurídico",
        "Logística e transporte",
        "Marketing e comunicação",
        "Recursos humanos",
        "Saúde e bem-estar",
        "Serviços gerais",
        "Tecnologia da informação",
        "Varejo e lojas",
        "Outras áreas"
    ];
}
