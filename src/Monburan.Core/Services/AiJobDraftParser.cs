using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;

namespace Monburan.Core.Services;

/// <summary>
/// Lê a resposta da IA para o rascunho da vaga. Só aceita valores que passam nas regras do domínio; o que não passa
/// vira campo vazio e entra na revisão. O leitor nunca completa um campo por conta própria.
/// </summary>
public static partial class AiJobDraftParser
{
    public const string Title = "title";
    public const string CompanyName = "companyName";
    public const string City = "city";
    public const string State = "state";
    public const string Description = "description";
    public const string Requirements = "requirements";
    public const string Benefits = "benefits";
    public const string SalaryMin = "salaryMin";
    public const string SalaryMax = "salaryMax";
    public const string SalaryPeriod = "salaryPeriod";
    public const string Deadline = "deadline";
    public const string Channel = "channel";
    public const string ChannelDetail = "channelDetail";
    public const string Instructions = "instructions";
    public const string CboCode = "cboCode";

    public static readonly IReadOnlyList<string> Fields =
    [
        Title, CompanyName, City, State, Description, Requirements, Benefits, SalaryMin, SalaryMax,
        SalaryPeriod, Deadline, Channel, ChannelDetail, Instructions, CboCode
    ];

    /// <summary>Campos que, sem valor, obrigam a revisão do rascunho.</summary>
    private static readonly string[] Essential = [Title, CompanyName, Description, Channel];

    private static readonly IReadOnlyDictionary<string, EApplicationChannel> ChannelMap = new Dictionary<string, EApplicationChannel>(StringComparer.OrdinalIgnoreCase)
    {
        ["externalUrl"] = EApplicationChannel.ExternalUrl,
        ["whatsapp"] = EApplicationChannel.WhatsApp,
        ["email"] = EApplicationChannel.Email,
        ["onSite"] = EApplicationChannel.OnSite
    };

    private static readonly IReadOnlyDictionary<string, ESalaryPeriod> PeriodMap = new Dictionary<string, ESalaryPeriod>(StringComparer.OrdinalIgnoreCase)
    {
        ["hour"] = ESalaryPeriod.Hour,
        ["day"] = ESalaryPeriod.Day,
        ["week"] = ESalaryPeriod.Week,
        ["month"] = ESalaryPeriod.Month,
        ["year"] = ESalaryPeriod.Year
    };

    /// <summary>Lê o JSON devolvido pelo modelo. Devolve nulo quando a resposta não é um objeto JSON utilizável.</summary>
    public static AiJobDraft? Parse(string content, DateOnly today)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var fields = new Dictionary<string, AiDraftField>(StringComparer.Ordinal);
            foreach (var name in Fields)
            {
                var raw = Read(document.RootElement, name, out var confidence);
                var value = Normalize(name, raw, today);
                fields[name] = value is null
                    ? new AiDraftField(null, "none")
                    : new AiDraftField(value, confidence);
            }

            var needsReview = Essential.Any(name => fields[name].Value is null)
                || fields.Values.Any(field => field.Value is not null && field.Confidence == "low");

            return new AiJobDraft(fields, needsReview);
        }
    }

    private static string? Read(JsonElement root, string name, out string confidence)
    {
        confidence = "low";
        if (!root.TryGetProperty(name, out var element))
            return null;

        if (element.ValueKind == JsonValueKind.Object)
        {
            confidence = element.TryGetProperty("confidence", out var level) && level.ValueKind == JsonValueKind.String
                ? NormalizeConfidence(level.GetString())
                : "low";
            return element.TryGetProperty("value", out var value) ? ToText(value) : null;
        }

        return ToText(element);
    }

    private static string NormalizeConfidence(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "high" => "high",
        "medium" => "medium",
        _ => "low"
    };

    private static string? ToText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        _ => null
    };

    /// <summary>Aplica as regras de cada campo. Valor fora da regra vira nulo (e a revisão aparece).</summary>
    private static string? Normalize(string name, string? raw, DateOnly today)
    {
        var text = raw?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;

        return name switch
        {
            SalaryMin or SalaryMax => ParseMoney(text) is { } amount && amount > 0 ? amount.ToString(CultureInfo.InvariantCulture) : null,
            SalaryPeriod => PeriodMap.TryGetValue(text, out var period) ? period.ToString() : null,
            Deadline => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date >= today
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null,
            Channel => ChannelMap.TryGetValue(text, out var channel) ? channel.ToString() : null,
            CboCode => CboPattern().IsMatch(text) ? text : null,
            State => text.Length == 2 ? text.ToUpperInvariant() : text,
            _ => text
        };
    }

    /// <summary>
    /// Aceita "8000", "8000.50", "8.000", "8.000,50" e "R$ 8.000". Milhar com ponto só é lido como milhar quando o
    /// formato bate com o padrão brasileiro; "8.000" sozinho não vira 8.
    /// </summary>
    private static decimal? ParseMoney(string text)
    {
        var normalized = text.Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(" ", string.Empty);
        if (ThousandsPattern().IsMatch(normalized))
            normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
        else if (normalized.Contains(',') && !normalized.Contains('.'))
            normalized = normalized.Replace(',', '.');

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    }

    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+(,\d{1,2})?$")]
    private static partial Regex ThousandsPattern();

    [GeneratedRegex(@"^\d{4}(-\d{2})?$")]
    private static partial Regex CboPattern();
}
