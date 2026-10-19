using Monburan.Core.Entities;

namespace Monburan.Core.Services;

/// <summary>
/// Casamento entre uma vaga recém-publicada e um alerta (contrato da trilha do candidato): regra pura, sem banco.
/// Palavras-chave (qualquer uma), cidade, UF, modalidade, categoria e salário mínimo precisam bater quando preenchidos.
/// </summary>
public static class JobAlertMatcher
{
    private static readonly char[] Separators = [',', ';', ' ', '\t', '\n', '\r'];

    public static bool Matches(Job job, JobAlert alert)
    {
        if (!alert.IsActive)
            return false;

        return KeywordsMatch(job, alert.Keywords)
            && CityMatches(job, alert.City)
            && StateMatches(job, alert.State)
            && ModeMatches(job, alert.Mode)
            && CategoryMatches(job, alert.Category)
            && SalaryMatches(job, alert.SalaryMin);
    }

    private static bool KeywordsMatch(Job job, string? keywords)
    {
        var terms = SplitTerms(keywords);
        if (terms.Count == 0)
            return true;

        var haystack = $"{job.Title}\n{job.Tags}\n{job.Category}\n{job.Description}";
        return terms.Any(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> SplitTerms(string? keywords) =>
        string.IsNullOrWhiteSpace(keywords)
            ? []
            : keywords.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static bool CityMatches(Job job, string? city) =>
        string.IsNullOrWhiteSpace(city)
        || (job.Location?.City is { } jobCity && string.Equals(city.Trim(), jobCity.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool StateMatches(Job job, string? state) =>
        string.IsNullOrWhiteSpace(state)
        || (job.Location?.State is { } jobState && string.Equals(state.Trim(), jobState.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool ModeMatches(Job job, string? mode) =>
        string.IsNullOrWhiteSpace(mode)
        || (job.Location?.Type is { } jobMode && string.Equals(mode.Trim(), jobMode.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool CategoryMatches(Job job, string? category) =>
        string.IsNullOrWhiteSpace(category)
        || (job.Category is { } jobCategory && string.Equals(category.Trim(), jobCategory.Trim(), StringComparison.OrdinalIgnoreCase));

    // Só há casamento de salário quando a vaga informa um valor; o maior informado (ou o menor, sem teto) precisa atingir o mínimo do alerta
    private static bool SalaryMatches(Job job, decimal? salaryMin)
    {
        if (salaryMin is not > 0)
            return true;

        var reference = job.Salary?.Max ?? job.Salary?.Min;
        return reference is { } value && value >= salaryMin.Value;
    }
}
