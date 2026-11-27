using System.Text.Json;
using System.Text.RegularExpressions;

using Monburan.Core.Entities;

namespace Monburan.Core.UseCases.Resumes;

/// <summary>Regras compartilhadas das variantes localizadas de currículo (MI-02).</summary>
internal static class ResumeVariantLocalization
{
    // BCP-47 simples, igual aos idiomas do candidato: "es", "es-ES", "en-US"…
    private static readonly Regex LocaleFormat = new("^[a-z]{2,3}(-[A-Z]{2})?$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal const int MaxOverrides = 300;
    internal const int MaxOverrideKeyLength = 80;
    internal const int MaxOverrideValueLength = 4000;
    internal const string FeatureName = "carreira-internacional";

    /// <summary>Chave de override válida — mesmo formato documentado em <see cref="ResumeVariant"/>.</summary>
    private static readonly Regex KeyFormat = new(
        "^(title|summary|experience:[0-9]+:(position|company|description)|education:[0-9]+:(institution|course)|skills:[0-9]+|languages:[0-9]+)$",
        RegexOptions.Compiled);

    public static bool IsValidLocale(string locale) => LocaleFormat.IsMatch(locale);

    public static bool IsValidKey(string key) =>
        key.Length <= MaxOverrideKeyLength && KeyFormat.IsMatch(key);

    public static Dictionary<string, string> ParseOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(Dictionary<string, string> overrides) =>
        JsonSerializer.Serialize(overrides, JsonOptions);

    /// <summary>Texto localizado quando existe override; senão o original do currículo base.</summary>
    public static string Localize(Dictionary<string, string> overrides, string key, string? baseValue) =>
        overrides.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : baseValue ?? string.Empty;
}
