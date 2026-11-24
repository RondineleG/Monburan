using Monburan.Core.DTOs.Response;

namespace Monburan.Core.Services;

/// <summary>Um texto do currículo que pode ganhar tradução na versão localizada.</summary>
public sealed record ResumeVariantField(string Key, string Group, string Label, string Original, bool Multiline);

/// <summary>
/// Campos traduzíveis do currículo na mesma ordem e com as mesmas chaves do backend (ResumeVariant): a tradução de
/// "experience:0:position" é a do primeiro cargo, na ordem em que o currículo-base os guarda.
/// </summary>
public static class ResumeVariantFields
{
    /// <summary>Idiomas oferecidos agora. O PRD valida a Espanha primeiro; outros entram depois dela.</summary>
    public static readonly IReadOnlyList<(string Locale, string Label)> Locales = [("es-ES", "Espanhol (Espanha)")];

    public static string LocaleLabel(string locale) =>
        Locales.FirstOrDefault(item => string.Equals(item.Locale, locale, StringComparison.OrdinalIgnoreCase)).Label ?? locale;

    public static IReadOnlyList<ResumeVariantField> From(ResumeResponse resume)
    {
        var fields = new List<ResumeVariantField>
        {
            new("title", "Resumo profissional", "Título", resume.Title, false)
        };
        if (!string.IsNullOrWhiteSpace(resume.Summary))
            fields.Add(new("summary", "Resumo profissional", "Resumo", resume.Summary, true));

        var experience = resume.ExperienceHistory ?? [];
        for (var i = 0; i < experience.Count; i++)
        {
            var group = $"Experiência {i + 1}: {experience[i].Position}";
            fields.Add(new($"experience:{i}:position", group, "Cargo", experience[i].Position, false));
            fields.Add(new($"experience:{i}:company", group, "Empresa", experience[i].Company, false));
            if (!string.IsNullOrWhiteSpace(experience[i].Description))
                fields.Add(new($"experience:{i}:description", group, "Atividades", experience[i].Description!, true));
        }

        var education = resume.EducationHistory ?? [];
        for (var i = 0; i < education.Count; i++)
        {
            var group = $"Formação {i + 1}: {education[i].Course}";
            fields.Add(new($"education:{i}:course", group, "Curso", education[i].Course, false));
            fields.Add(new($"education:{i}:institution", group, "Instituição", education[i].Institution, false));
        }

        var skills = resume.Skills ?? [];
        for (var i = 0; i < skills.Count; i++)
            fields.Add(new($"skills:{i}", "Habilidades", $"Habilidade {i + 1}", skills[i].Skill, false));

        var languages = resume.Languages ?? [];
        for (var i = 0; i < languages.Count; i++)
            fields.Add(new($"languages:{i}", "Idiomas", $"Idioma {i + 1}", languages[i].Language, false));

        return fields;
    }

    /// <summary>
    /// Currículo para o documento de CV com os textos traduzidos no lugar dos originais (datas, níveis e estrutura
    /// continuam os da base). Sem tradução, fica o texto original.
    /// </summary>
    public static ResumeResponse Localize(ResumeResponse resume, IReadOnlyDictionary<string, string> overrides)
    {
        string Text(string key, string original) =>
            overrides.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : original;

        return new ResumeResponse
        {
            Id = resume.Id,
            CandidateId = resume.CandidateId,
            Title = Text("title", resume.Title),
            Summary = resume.Summary is null ? null : Text("summary", resume.Summary),
            IsActive = resume.IsActive,
            CreatedAt = resume.CreatedAt,
            UpdatedAt = resume.UpdatedAt,
            ExperienceHistory = resume.ExperienceHistory?.Select((item, i) => new ExperienceResponse
            {
                Company = Text($"experience:{i}:company", item.Company),
                Position = Text($"experience:{i}:position", item.Position),
                Description = item.Description is null ? null : Text($"experience:{i}:description", item.Description),
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                IsCurrent = item.IsCurrent
            }).ToList(),
            EducationHistory = resume.EducationHistory?.Select((item, i) => new EducationResponse
            {
                Institution = Text($"education:{i}:institution", item.Institution),
                Course = Text($"education:{i}:course", item.Course),
                Level = item.Level,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                Status = item.Status
            }).ToList(),
            Skills = resume.Skills?.Select((item, i) => new SkillResponse
            {
                Skill = Text($"skills:{i}", item.Skill),
                Level = item.Level,
                YearsOfExperience = item.YearsOfExperience
            }).ToList(),
            Languages = resume.Languages?.Select((item, i) => new LanguageResponse
            {
                Language = Text($"languages:{i}", item.Language),
                Level = item.Level
            }).ToList()
        };
    }
}
