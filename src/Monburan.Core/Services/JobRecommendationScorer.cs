using Monburan.Core.Entities;

namespace Monburan.Core.Services;

/// <summary>
/// Pontuação de vagas para o candidato (contrato da trilha do candidato): regra pura, sem banco.
/// +3 cargo desejado no título/tags/categoria, +2 categoria, +2 cidade/estado, +1 modalidade,
/// +2 salário que atinge o mínimo, +1 por habilidade do currículo ativo (máximo +3).
/// </summary>
public static class JobRecommendationScorer
{
    public const int MaxSkillBonus = 3;

    private const int MinWordLength = 3;

    public static int Score(Job job, CareerProfile? profile, IReadOnlyCollection<string> skills)
    {
        var score = 0;

        if (profile is not null)
        {
            score += ScoreDesiredRole(job, profile.DesiredRole);
            score += ScoreCategory(job, profile.Category);
            score += ScoreLocation(job, profile.City, profile.State);
            score += ScoreMode(job, profile.Mode);
            score += ScoreSalary(job, profile.MinimumSalary);
        }

        score += ScoreSkills(job, skills);

        return score;
    }

    // Qualquer palavra do cargo (3+ letras) que apareça no título, nas tags ou na categoria pontua
    private static int ScoreDesiredRole(Job job, string? desiredRole)
    {
        if (string.IsNullOrWhiteSpace(desiredRole))
            return 0;

        var words = desiredRole
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length >= MinWordLength)
            .Select(word => word.ToLowerInvariant())
            .ToList();
        if (words.Count == 0)
            return 0;

        var fields = new[] { job.Title, job.Tags, job.Category }
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field!.ToLowerInvariant())
            .ToList();

        return words.Any(word => fields.Any(field => field.Contains(word))) ? 3 : 0;
    }

    private static int ScoreCategory(Job job, string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(job.Category))
            return 0;

        return string.Equals(category.Trim(), job.Category.Trim(), StringComparison.OrdinalIgnoreCase) ? 2 : 0;
    }

    private static int ScoreLocation(Job job, string? city, string? state)
    {
        var cityMatch = !string.IsNullOrWhiteSpace(city)
            && job.Location?.City is { } jobCity
            && string.Equals(city.Trim(), jobCity.Trim(), StringComparison.OrdinalIgnoreCase);
        var stateMatch = !string.IsNullOrWhiteSpace(state)
            && job.Location?.State is { } jobState
            && string.Equals(state.Trim(), jobState.Trim(), StringComparison.OrdinalIgnoreCase);

        return cityMatch || stateMatch ? 2 : 0;
    }

    private static int ScoreMode(Job job, string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode) || string.IsNullOrWhiteSpace(job.Location?.Type))
            return 0;

        return string.Equals(mode.Trim(), job.Location!.Type!.Trim(), StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    // Só pontua quando a vaga informa salário; o maior valor (ou o menor, sem teto) precisa atingir o mínimo pedido
    private static int ScoreSalary(Job job, decimal? minimumSalary)
    {
        if (minimumSalary is not > 0)
            return 0;

        var reference = job.Salary?.Max ?? job.Salary?.Min;
        return reference is { } value && value >= minimumSalary.Value ? 2 : 0;
    }

    private static int ScoreSkills(Job job, IReadOnlyCollection<string> skills)
    {
        if (skills.Count == 0)
            return 0;

        var haystack = $"{job.Title}\n{job.Tags}\n{job.Description}";
        var hits = skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count(skill => haystack.Contains(skill, StringComparison.OrdinalIgnoreCase));

        return Math.Min(hits, MaxSkillBonus);
    }
}
