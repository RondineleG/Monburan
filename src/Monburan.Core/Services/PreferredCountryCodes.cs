namespace Monburan.Core.Services;

/// <summary>Normalização da lista de países preferidos (ISO alpha-2 em CSV) do perfil de carreira.</summary>
public static class PreferredCountryCodes
{
    /// <summary>CSV → lista normalizada (maiúsculas, sem duplicatas, sem espaços).</summary>
    public static List<string>? Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return null;

        var codes = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToUpperInvariant())
            .Distinct()
            .ToList();

        return codes.Count == 0 ? null : codes;
    }

    /// <summary>Lista → CSV normalizado para persistência.</summary>
    public static string? Serialize(IEnumerable<string>? codes)
    {
        if (codes is null)
            return null;

        var normalized = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        return normalized.Count == 0 ? null : string.Join(',', normalized);
    }
}
