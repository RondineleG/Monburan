using System.Globalization;
using Monburan.Core.DTOs.Response;

namespace Monburan.Web.Services;

/// <summary>
/// Textos de exibição de vagas, iguais no cartão da lista e na página da vaga. As regras de salário seguem o
/// <c>Formatters</c> do app (Monburan.Mobile.Shared), para o app reproduzir a mesma tela.
/// </summary>
public static class JobDisplay
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Faixa salarial ("R$ 8.000 – 12.000") ou <c>null</c> quando a empresa ocultou ou não informou.</summary>
    public static string? Salary(JobResponse job)
    {
        if (!job.SalaryDisplay || (!job.SalaryMin.HasValue && !job.SalaryMax.HasValue))
            return null;

        var symbol = UiLabels.CurrencySymbol(job.SalaryCurrency);
        var min = job.SalaryMin is { } low ? Money(low) : null;
        var max = job.SalaryMax is { } high ? Money(high) : null;

        return (min, max) switch
        {
            (not null, not null) when job.SalaryMin == job.SalaryMax => $"{symbol} {min}",
            (not null, not null) => $"{symbol} {min} – {max}",
            (not null, null) => $"A partir de {symbol} {min}",
            _ => $"Até {symbol} {max}"
        };
    }

    /// <summary>
    /// Cidade e UF; sem endereço, vaga remota vira "Brasil" e as demais "Local a combinar".
    /// A vaga pode não estar carregada (a página abre antes do carregamento): nesse caso,
    /// devolve o texto de "local a combinar".
    /// </summary>
    public static string Location(JobResponse? job)
    {
        if (job is null)
            return "Local a combinar";

        var city = job.City?.Trim();
        var state = job.State?.Trim();

        if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(state))
            return $"{city}, {state}";
        if (!string.IsNullOrEmpty(city))
            return city;
        if (!string.IsNullOrEmpty(state))
            return state;

        return job.IsRemote || job.Type == "Remote" ? "Brasil" : "Local a combinar";
    }

    /// <summary>Nome do ícone (<c>Icon</c>) da modalidade de trabalho.</summary>
    public static string ModeIcon(string? type) => type switch
    {
        "Remote" => "home",
        "Hybrid" => "shuffle",
        _ => "building"
    };

    /// <summary>Inicial exibida no lugar do logotipo da empresa.</summary>
    public static string Initial(string? companyName) =>
        string.IsNullOrWhiteSpace(companyName) ? "M" : companyName.Trim()[..1].ToUpperInvariant();

    /// <summary>Dias de calendário (em Brasília) desde a publicação.</summary>
    public static int AgeInDays(DateTime createdAt, DateTime? nowUtc = null) =>
        (int)(UiLabels.ToBrasilia(nowUtc ?? DateTime.UtcNow).Date - UiLabels.ToBrasilia(createdAt).Date).TotalDays;

    /// <summary>"hoje", "ontem", "há 3 dias", "há 2 semanas", "há 3 meses" (para "Publicada ...").</summary>
    public static string Age(DateTime createdAt, DateTime? nowUtc = null)
    {
        var days = AgeInDays(createdAt, nowUtc);
        return days switch
        {
            <= 0 => "hoje",
            1 => "ontem",
            < 7 => $"há {days} dias",
            < 14 => "há 1 semana",
            < 30 => $"há {days / 7} semanas",
            < 60 => "há 1 mês",
            < 365 => $"há {days / 30} meses",
            _ => "há mais de 1 ano"
        };
    }

    /// <summary>Vaga publicada hoje, ontem ou anteontem.</summary>
    public static bool IsNew(DateTime createdAt, DateTime? nowUtc = null) => AgeInDays(createdAt, nowUtc) <= 2;

    /// <summary>
    /// Aviso de encerramento próximo ("Encerra amanhã", "Encerra em 3 dias") nos últimos 7 dias de
    /// validade; <c>null</c> fora disso. A validade é uma data de calendário, sem fuso.
    /// </summary>
    public static string? ClosingNotice(DateTime expirationDate, DateTime? nowUtc = null)
    {
        if (expirationDate == default)
            return null;

        var days = (int)(expirationDate.Date - UiLabels.ToBrasilia(nowUtc ?? DateTime.UtcNow).Date).TotalDays;
        return days switch
        {
            < 0 => null,
            0 => "Encerra hoje",
            1 => "Encerra amanhã",
            <= 7 => $"Encerra em {days} dias",
            _ => null
        };
    }

    /// <summary>Etiquetas da vaga (campo separado por vírgulas), sem repetição.</summary>
    public static IReadOnlyList<string> Tags(string? tags, int max = int.MaxValue) =>
        string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();

    /// <summary>Trecho de texto em uma linha, cortado no fim de uma palavra.</summary>
    public static string Excerpt(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (flat.Length <= maxLength)
            return flat;

        var cut = flat.LastIndexOf(' ', maxLength);
        var end = cut > maxLength / 2 ? cut : maxLength;
        return flat[..end].TrimEnd(',', '.', ';', ':', '-', ' ') + "…";
    }

    /// <summary>Link do WhatsApp com o título e o endereço da vaga.</summary>
    public static string WhatsAppUrl(string title, string url) =>
        $"https://wa.me/?text={Uri.EscapeDataString($"{title} — {url}")}";

    /// <summary>Valor em reais no padrão pt-BR, sem centavos quando é inteiro ("8.000" ou "3.250,50").</summary>
    public static string Money(decimal value) =>
        value % 1 == 0 ? value.ToString("N0", PtBr) : value.ToString("N2", PtBr);
}
