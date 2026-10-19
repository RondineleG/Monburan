namespace Monburan.Core.Services;

/// <summary>Modalidades de trabalho dos alertas e do perfil de carreira — mesmos valores de <c>JobLocation.Type</c>.</summary>
public static class JobModes
{
    public const string OnSite = "OnSite";
    public const string Remote = "Remote";
    public const string Hybrid = "Hybrid";

    public static IReadOnlyList<string> All { get; } = [OnSite, Remote, Hybrid];

    /// <summary>Devolve o valor do catálogo (com a caixa certa) ou <c>null</c> quando não é uma modalidade válida.</summary>
    public static string? Normalize(string? mode) =>
        string.IsNullOrWhiteSpace(mode)
            ? null
            : All.FirstOrDefault(value => string.Equals(value, mode.Trim(), StringComparison.OrdinalIgnoreCase));
}
