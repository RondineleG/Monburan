namespace Monburan.Web.Services;

/// <summary>Interpretação dos campos livres da busca de vagas.</summary>
public static class JobSearch
{
    private static readonly HashSet<string> States = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    };

    private static readonly HashSet<string> RemoteKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "remoto", "remote", "home office", "homeoffice", "trabalho remoto"
    };

    private static readonly char[] LocationSeparators = [',', '-', '/', '–'];

    /// <summary>"Remoto" digitado no campo de local vale como o filtro de modalidade.</summary>
    public static bool IsRemoteKeyword(string? text) =>
        !string.IsNullOrWhiteSpace(text) && RemoteKeywords.Contains(text.Trim());

    /// <summary>
    /// Separa o campo único "cidade ou estado": "SP" é só o estado; "Campinas, SP", "Campinas - SP" e
    /// "Campinas/SP" trazem cidade e estado; qualquer outro texto é cidade.
    /// </summary>
    public static void ParseLocation(string? text, out string city, out string state)
    {
        city = string.Empty;
        state = string.Empty;

        var value = text?.Trim();
        if (string.IsNullOrEmpty(value))
            return;

        if (States.Contains(value))
        {
            state = value.ToUpperInvariant();
            return;
        }

        var cut = value.LastIndexOfAny(LocationSeparators);
        if (cut > 0)
        {
            var tail = value[(cut + 1)..].Trim();
            if (States.Contains(tail))
            {
                city = value[..cut].Trim();
                state = tail.ToUpperInvariant();
                return;
            }
        }

        city = value;
    }
}
