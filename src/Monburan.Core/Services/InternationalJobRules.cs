using System.Text.RegularExpressions;

using Monburan.Core.Entities;
using Monburan.Core.Enums;

namespace Monburan.Core.Services;

/// <summary>
/// Informações de carreira internacional da vaga: país, moeda, abrangência do remoto, países elegíveis, relocação e
/// patrocínio de visto. Ausência é "não informado" (nulo), nunca "não": a tela distingue os dois e não deduz nada.
/// </summary>
public static partial class InternationalJobRules
{
    /// <summary>Feature flag dos campos internacionais no cadastro de vaga (entra desligada).</summary>
    public const string FeatureKey = "carreira-internacional";

    public const int MaxEligibleCountries = 30;

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryCode();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyCode();

    public static bool IsCountryCode(string? value) => value is not null && CountryCode().IsMatch(value);

    public static bool IsCurrencyCode(string? value) => value is not null && CurrencyCode().IsMatch(value);

    /// <summary>Códigos ISO de país em maiúsculas, sem repetição, separados por vírgula, ponto e vírgula ou espaço.</summary>
    public static IReadOnlyList<string> ParseCountries(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(code => code.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();

    /// <summary>
    /// Deixa a vaga coerente antes de validar: códigos em maiúsculas; a abrangência só existe em vaga remota ou híbrida;
    /// a lista de países só existe quando o remoto é restrito.
    /// </summary>
    public static void Normalize(Job job)
    {
        if (job.Location is { } location)
            location.Country = string.IsNullOrWhiteSpace(location.Country) ? "BR" : location.Country.Trim().ToUpperInvariant();
        if (job.Salary is { } salary)
            salary.Currency = string.IsNullOrWhiteSpace(salary.Currency) ? "BRL" : salary.Currency.Trim().ToUpperInvariant();

        if (job.Location is not { Type: "Remote" or "Hybrid" })
            job.RemoteScope = null;

        var countries = ParseCountries(job.EligibleCountries);
        job.EligibleCountries = job.RemoteScope == ERemoteScope.Restricted && countries.Count > 0
            ? string.Join(",", countries)
            : null;
    }

    /// <summary>Erros de campo (mensagem, campo) da parte internacional; vazio quando está tudo certo.</summary>
    public static IEnumerable<(string Message, string Field)> Validate(Job job)
    {
        if (job.Location is { } location && !IsCountryCode(location.Country))
            yield return ("Informe o país com o código de duas letras (ex.: BR, PT).", "Country");

        if (job.Salary is { } salary && !IsCurrencyCode(salary.Currency))
            yield return ("Informe a moeda com o código de três letras (ex.: BRL, EUR).", "SalaryCurrency");

        if (job.RemoteScope != ERemoteScope.Restricted)
            yield break;

        var countries = ParseCountries(job.EligibleCountries);
        if (countries.Count == 0)
            yield return ("Informe os países de onde a pessoa pode trabalhar.", "EligibleCountries");
        else if (countries.Count > MaxEligibleCountries)
            yield return ($"Informe no máximo {MaxEligibleCountries} países.", "EligibleCountries");
        else if (countries.FirstOrDefault(code => !IsCountryCode(code)) is { } invalid)
            yield return ($"\"{invalid}\" não é um código de país de duas letras.", "EligibleCountries");
    }
}
