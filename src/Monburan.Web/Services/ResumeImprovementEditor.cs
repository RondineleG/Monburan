using System.Text.Json;
using System.Text.RegularExpressions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Services;
using Monburan.Web.Models;

namespace Monburan.Web.Services;

/// <summary>
/// Regras puras da revisão de sugestões de IA: monta o rascunho a partir do resultado,
/// casa sugestões de experiência com o currículo original, compara snapshots e monta
/// o UpdateResumeRequest / a prévia refletindo SÓ o que a pessoa selecionou e editou.
/// </summary>
public static class ResumeImprovementEditor
{
    // Lacuna que a IA deixa para a pessoa preencher: [X%], [X], [XX], [N], [??], [...],
    // [insira ...], [preencha ...]. Texto normal entre colchetes ("[projeto executivo]",
    // "[Linux]") não é lacuna: os tokens exigem formato próprio, não qualquer letra.
    private static readonly Regex Placeholder = new(
        @"\[\s*(?:[Xx]{1,3}%?|[Nn]|\?{1,3}|…|\.{3,}|(?:insira|preencha|informe|complete|adicione|seu|sua)\b[^\]]{0,40})\s*\]",
        RegexOptions.Compiled);

    /// <summary>Verdadeiro quando o texto ainda tem lacuna do tipo [X%] a preencher.</summary>
    public static bool ContainsPlaceholder(string? text) =>
        !string.IsNullOrEmpty(text) && Placeholder.IsMatch(text);

    /// <summary>
    /// Casa a sugestão a UMA experiência original pelo par exato empresa+cargo
    /// (ignora caixa e espaços nas pontas). Retorna o índice; -1 sem par; -2 ambíguo.
    /// </summary>
    public static int MatchExperienceIndex(IReadOnlyList<ExperienceResponse> originals, string? company, string? position)
    {
        static string Key(string? value) => value?.Trim() ?? string.Empty;

        var match = -1;
        var matches = 0;
        for (var i = 0; i < originals.Count; i++)
        {
            if (!string.Equals(Key(originals[i].Company), Key(company), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Key(originals[i].Position), Key(position), StringComparison.OrdinalIgnoreCase))
                continue;

            match = i;
            matches++;
        }

        return matches == 1 ? match : matches == 0 ? -1 : -2;
    }

    /// <summary>Monta o rascunho editável: cada sugestão nasce desmarcada e resolvida (ou não).</summary>
    public static ResumeImprovementDraft CreateDraft(Guid candidateId, ResumeResponse snapshot, ResumeImprovementResponse result)
    {
        var originals = snapshot.ExperienceHistory ?? [];
        var draft = new ResumeImprovementDraft
        {
            CandidateId = candidateId,
            ResumeId = snapshot.Id,
            ResumeSnapshot = snapshot,
            Result = result,
            SummarySelected = false,
            EditedSummary = result.Summary,
            GeneratedAtUtc = result.GeneratedAt,
            Skills = SkillNormalizer.NormalizeAll(result.SkillsToAdd)
                .Where(skill => !(snapshot.Skills ?? []).Any(existing => SkillNormalizer.SameAs(existing.Skill, skill)))
                .Select(skill => new ResumeImprovementDraftSkill { Skill = skill, Selected = false })
                .ToList(),
            Experiences = result.Experiences.Select(suggestion =>
            {
                var index = MatchExperienceIndex(originals, suggestion.Company, suggestion.Position);
                var matched = index >= 0;
                var originalDescription = matched ? originals[index].Description : null;
                return new ResumeImprovementDraftExperience
                {
                    Company = suggestion.Company,
                    Position = suggestion.Position,
                    MatchedIndex = matched ? index : null,
                    Selected = false,
                    EditedDescription = AppendSuggestions(originalDescription, suggestion.Suggestions)
                };
            }).ToList()
        };

        return draft;
    }

    /// <summary>Descrição original preservada + sugestões acrescentadas abaixo.</summary>
    public static string AppendSuggestions(string? originalDescription, IReadOnlyList<string> suggestions)
    {
        var additions = string.Join('\n', suggestions.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => $"• {s.Trim()}"));
        if (additions.Length == 0)
            return originalDescription ?? string.Empty;
        if (string.IsNullOrWhiteSpace(originalDescription))
            return additions;
        return $"{originalDescription.TrimEnd()}\n{additions}";
    }

    /// <summary>
    /// O currículo mudou desde a geração? Compara o conteúdo (título, resumo, ativo e
    /// as coleções). Mudou → conflito: nunca sobrescrever edições feitas depois.
    /// </summary>
    public static bool SnapshotChanged(ResumeResponse? before, ResumeResponse? latest) =>
        Canonical(before) != Canonical(latest);

    /// <summary>Campos selecionados que ainda têm lacuna [X%] — bloqueiam a aplicação.</summary>
    public static IReadOnlyList<string> PlaceholderBlocks(ResumeImprovementDraft draft)
    {
        var blocked = new List<string>();
        if (draft.SummarySelected && ContainsPlaceholder(draft.EditedSummary))
            blocked.Add("Resumo");

        foreach (var experience in draft.Experiences)
        {
            if (experience.Selected && ContainsPlaceholder(experience.EditedDescription))
                blocked.Add($"Experiência: {experience.Position}");
        }

        foreach (var skill in draft.Skills)
        {
            if (skill.Selected && ContainsPlaceholder(skill.Skill))
                blocked.Add($"Habilidade: {skill.Skill}");
        }

        return blocked;
    }

    /// <summary>Há alguma mudança aplicável? Seleção sem efeito nunca habilita o aplicar.</summary>
    public static bool HasApplicableChanges(ResumeImprovementDraft draft)
    {
        if (draft.SummarySelected && !string.IsNullOrWhiteSpace(draft.EditedSummary))
            return true;
        if (draft.Experiences.Any(e => e.Selected && e.MatchedIndex is not null))
            return true;
        return draft.Skills.Any(s => s.Selected && !string.IsNullOrWhiteSpace(s.Skill));
    }

    /// <summary>Prévia do currículo com apenas as seleções aplicadas (nada é gravado).</summary>
    public static ResumeResponse Preview(ResumeResponse latest, ResumeImprovementDraft draft)
    {
        var preview = new ResumeResponse
        {
            Id = latest.Id,
            CandidateId = latest.CandidateId,
            Title = latest.Title,
            Summary = draft.SummarySelected ? draft.EditedSummary : latest.Summary,
            IsActive = latest.IsActive,
            CreatedAt = latest.CreatedAt,
            UpdatedAt = latest.UpdatedAt,
            EducationHistory = CloneEducation(latest),
            ExperienceHistory = CloneExperiences(latest),
            Languages = CloneLanguages(latest),
            Skills = CloneSkills(latest)
        };

        foreach (var experience in draft.Experiences)
        {
            // Índice sempre dentro da lista: negativo ou fora do intervalo nunca aplica
            if (experience.Selected && experience.MatchedIndex is { } index
                && index >= 0 && index < (preview.ExperienceHistory?.Count ?? 0))
            {
                preview.ExperienceHistory![index].Description = experience.EditedDescription;
            }
        }

        if (preview.Skills is not null)
        {
            foreach (var skill in draft.Skills)
            {
                var normalized = SkillNormalizer.Normalize(skill.Skill);
                if (skill.Selected && normalized.Length > 0
                    && !preview.Skills.Any(existing => SkillNormalizer.SameAs(existing.Skill, normalized)))
                    preview.Skills.Add(new SkillResponse { Skill = normalized });
            }
        }

        return preview;
    }

    /// <summary>
    /// UpdateResumeRequest completo: título, situação, formação, datas e metadados de
    /// habilidades preservados — só os campos selecionados mudam.
    /// </summary>
    public static UpdateResumeRequest BuildUpdateRequest(ResumeResponse latest, ResumeImprovementDraft draft)
    {
        var request = new UpdateResumeRequest
        {
            Id = latest.Id,
            CandidateId = latest.CandidateId,
            Title = latest.Title,
            Summary = draft.SummarySelected ? draft.EditedSummary : latest.Summary,
            IsActive = latest.IsActive,
            EducationHistory = (latest.EducationHistory ?? []).Select(e => new EducationRequest
            {
                Institution = e.Institution,
                Course = e.Course,
                Level = Enum.TryParse<EEducationLevel>(e.Level, out var level) ? level : EEducationLevel.HighSchool,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Status = Enum.TryParse<EEducationStatus>(e.Status, out var status) ? status : EEducationStatus.Completed
            }).ToList(),
            ExperienceHistory = (latest.ExperienceHistory ?? []).Select(e => new ExperienceRequest
            {
                Company = e.Company,
                Position = e.Position,
                Description = e.Description,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                IsCurrent = e.IsCurrent
            }).ToList(),
            Languages = (latest.Languages ?? []).Select(l => new LanguageRequest
            {
                Language = l.Language,
                Level = Enum.TryParse<ELanguageLevel>(l.Level, out var level) ? level : ELanguageLevel.Intermediate
            }).ToList(),
            Skills = (latest.Skills ?? []).Select(s => new SkillRequest
            {
                Skill = s.Skill,
                Level = s.Level,
                YearsOfExperience = s.YearsOfExperience
            }).ToList()
        };

        foreach (var experience in draft.Experiences)
        {
            if (experience.Selected && experience.MatchedIndex is { } index
                && index >= 0 && index < (request.ExperienceHistory?.Count ?? 0))
            {
                request.ExperienceHistory![index].Description = experience.EditedDescription;
            }
        }

        foreach (var skill in draft.Skills)
        {
            var normalized = SkillNormalizer.Normalize(skill.Skill);
            if (skill.Selected && normalized.Length > 0
                && !request.Skills!.Any(existing => SkillNormalizer.SameAs(existing.Skill, normalized)))
                request.Skills!.Add(new SkillRequest { Skill = normalized });
        }

        return request;
    }

    // O snapshot guarda só o que decide conflito — SourceFileName/Url ficam de fora de propósito
    private static string Canonical(ResumeResponse? resume) => resume is null ? "<null>" : JsonSerializer.Serialize(new
    {
        resume.Title,
        resume.Summary,
        resume.IsActive,
        Education = resume.EducationHistory ?? [],
        Experiences = resume.ExperienceHistory ?? [],
        Languages = resume.Languages ?? [],
        Skills = (resume.Skills ?? []).Select(s => new { s.Skill, s.Level, s.YearsOfExperience }).ToList()
    });

    private static List<EducationResponse> CloneEducation(ResumeResponse resume) =>
        (resume.EducationHistory ?? []).Select(e => new EducationResponse
        {
            Institution = e.Institution,
            Course = e.Course,
            Level = e.Level,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Status = e.Status
        }).ToList();

    private static List<ExperienceResponse> CloneExperiences(ResumeResponse resume) =>
        (resume.ExperienceHistory ?? []).Select(e => new ExperienceResponse
        {
            Company = e.Company,
            Position = e.Position,
            Description = e.Description,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            IsCurrent = e.IsCurrent
        }).ToList();

    private static List<LanguageResponse> CloneLanguages(ResumeResponse resume) =>
        (resume.Languages ?? []).Select(l => new LanguageResponse { Language = l.Language, Level = l.Level }).ToList();

    private static List<SkillResponse> CloneSkills(ResumeResponse resume) =>
        (resume.Skills ?? []).Select(s => new SkillResponse { Skill = s.Skill, Level = s.Level, YearsOfExperience = s.YearsOfExperience }).ToList();
}
