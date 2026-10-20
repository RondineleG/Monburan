using System.Globalization;
using System.Text.Json;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;

namespace Monburan.Core.UseCases.Resumes;

/// <summary>
/// Lê a resposta da IA de extração com a mesma tolerância do parser de melhorias: aceita o JSON dentro
/// de texto, nomes em camelCase ou snake_case e datas em vários formatos ("2024-03", "03/2024",
/// "março de 2024", "2024"). Campos irreconhecíveis viram vazio em vez de quebrar a importação.
/// </summary>
public static class ResumeImportParser
{
    private const int MaxTitleLength = 150;
    private const int MaxSummaryLength = 2000;
    private const int MaxFieldLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const int MaxExperiences = 20;
    private const int MaxEducations = 10;
    private const int MaxLanguages = 10;
    private const int MaxSkills = 50;

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "yyyy-MM", "MM/yyyy", "MM-yyyy", "yyyy", "MMMM 'de' yyyy", "MMM 'de' yyyy", "MMMM yyyy", "MMM yyyy"];

    private static readonly CultureInfo PtBr = new("pt-BR");

    public static bool TryParse(string? content, out ImportedResumeDraft? draft)
    {
        draft = null;

        if (string.IsNullOrWhiteSpace(content))
            return false;

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;

        try
        {
            using var document = JsonDocument.Parse(content[start..(end + 1)]);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var parsed = new ImportedResumeDraft
            {
                Title = Clip(Text(Find(root, "title")), MaxTitleLength),
                Summary = Clip(Text(Find(root, "summary")), MaxSummaryLength),
                Experiences = Experiences(Find(root, "experiences")),
                Education = Educations(Find(root, "education") ?? Find(root, "educations")),
                Languages = Languages(Find(root, "languages")),
                Skills = Skills(Find(root, "skills")),
                ExtractedBy = EResumeImportMode.Ai
            };

            draft = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<ImportedExperience> Experiences(JsonElement? element)
    {
        var result = new List<ImportedExperience>();
        if (element is not { ValueKind: JsonValueKind.Array } array)
            return result;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var experience = new ImportedExperience
            {
                Company = Clip(Text(Find(item, "company")) ?? Text(Find(item, "employer")), MaxFieldLength),
                Position = Clip(Text(Find(item, "position")) ?? Text(Find(item, "role")) ?? Text(Find(item, "title")), MaxFieldLength),
                Description = Clip(Text(Find(item, "description")), MaxDescriptionLength),
                StartDate = Date(Find(item, "startDate") ?? Find(item, "start")),
                EndDate = Date(Find(item, "endDate") ?? Find(item, "end")),
                IsCurrent = Bool(Find(item, "isCurrent") ?? Find(item, "current"))
            };

            if (experience.IsCurrent)
                experience.EndDate = null;

            if (experience.Company.Length > 0 || experience.Position.Length > 0)
                result.Add(experience);

            if (result.Count == MaxExperiences)
                break;
        }

        return result;
    }

    private static List<ImportedEducation> Educations(JsonElement? element)
    {
        var result = new List<ImportedEducation>();
        if (element is not { ValueKind: JsonValueKind.Array } array)
            return result;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var education = new ImportedEducation
            {
                Institution = Clip(Text(Find(item, "institution")) ?? Text(Find(item, "school")), MaxFieldLength),
                Course = Clip(Text(Find(item, "course")) ?? Text(Find(item, "degree")), MaxFieldLength),
                Level = EducationLevel(Text(Find(item, "level"))),
                Status = EducationStatus(Text(Find(item, "status"))),
                StartDate = Date(Find(item, "startDate") ?? Find(item, "start")),
                EndDate = Date(Find(item, "endDate") ?? Find(item, "end"))
            };

            if (education.Institution.Length > 0 || education.Course.Length > 0)
                result.Add(education);

            if (result.Count == MaxEducations)
                break;
        }

        return result;
    }

    private static List<ImportedLanguage> Languages(JsonElement? element)
    {
        var result = new List<ImportedLanguage>();
        if (element is not { ValueKind: JsonValueKind.Array } array)
            return result;

        foreach (var item in array.EnumerateArray())
        {
            string name;
            ELanguageLevel level = ELanguageLevel.Intermediate;

            if (item.ValueKind == JsonValueKind.String)
            {
                name = item.GetString() ?? string.Empty;
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                name = Text(Find(item, "name")) ?? Text(Find(item, "language")) ?? string.Empty;
                level = LanguageLevel(Text(Find(item, "level")));
            }
            else
            {
                continue;
            }

            name = Clip(name, 60);
            if (name.Length == 0)
                continue;

            result.Add(new ImportedLanguage { Language = name, Level = level });
            if (result.Count == MaxLanguages)
                break;
        }

        return result;
    }

    private static List<ImportedSkill> Skills(JsonElement? element)
    {
        var result = new List<ImportedSkill>();
        if (element is not { ValueKind: JsonValueKind.Array } array)
            return result;

        foreach (var item in array.EnumerateArray())
        {
            string name;
            var level = 3;
            var years = 0;

            if (item.ValueKind == JsonValueKind.String)
            {
                name = item.GetString() ?? string.Empty;
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                name = Text(Find(item, "name")) ?? Text(Find(item, "skill")) ?? string.Empty;
                level = Int(Find(item, "level"), 3);
                years = Int(Find(item, "yearsOfExperience") ?? Find(item, "years"), 0);
            }
            else
            {
                continue;
            }

            name = Services.SkillNormalizer.Normalize(name);
            if (name.Length == 0)
                continue;

            level = Math.Clamp(level, 1, 5);
            years = Math.Clamp(years, 0, 60);

            if (!result.Any(existing => Services.SkillNormalizer.SameAs(existing.Skill, name)))
                result.Add(new ImportedSkill { Skill = name, Level = level, YearsOfExperience = years });

            if (result.Count == MaxSkills)
                break;
        }

        return result;
    }

    private static JsonElement? Find(JsonElement element, string name)
    {
        var wanted = name.Replace("_", string.Empty).Replace("-", string.Empty);
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name.Replace("_", string.Empty).Replace("-", string.Empty), wanted, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }

    private static string? Text(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.String } text => text.GetString(),
        { ValueKind: JsonValueKind.Number } number => number.GetRawText(),
        _ => null
    };

    private static bool Bool(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.String } text => text.GetString() is { } value
            && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("sim", StringComparison.OrdinalIgnoreCase)),
        _ => false
    };

    private static int Int(JsonElement? element, int fallback) => element switch
    {
        { ValueKind: JsonValueKind.Number } number when number.TryGetInt32(out var value) => value,
        { ValueKind: JsonValueKind.String } text when int.TryParse(text.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
        _ => fallback
    };

    private static DateTime? Date(JsonElement? element)
    {
        var value = Text(element);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return ResumeTextHeuristics.ParseDate(value) ?? (DateTime.TryParseExact(value.Trim(), DateFormats, PtBr, DateTimeStyles.None, out var parsed) ? parsed : null);
    }

    private static EEducationLevel EducationLevel(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "elementary" or "fundamental" or "ensino fundamental" => EEducationLevel.Elementary,
        "highschool" or "high_school" or "ensino medio" or "ensino médio" or "medio" or "médio" => EEducationLevel.HighSchool,
        "technical" or "tecnico" or "técnico" or "tecnologo" or "tecnólogo" or "cst" or "curso superior de tecnologia" => EEducationLevel.Technical,
        "postgraduate" or "post_graduate" or "pos-graduacao" or "pós-graduação" or "pos" or "pós" or "mba" or "especializacao" or "especialização" => EEducationLevel.Postgraduate,
        "master" or "msc" or "mestrado" => EEducationLevel.Master,
        "doctorate" or "phd" or "doutorado" => EEducationLevel.Doctorate,
        _ => EEducationLevel.Bachelor
    };

    private static EEducationStatus EducationStatus(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "ongoing" or "cursando" or "em andamento" or "andamento" => EEducationStatus.Ongoing,
        "dropped" or "trancado" or "interrompido" => EEducationStatus.Dropped,
        "incomplete" or "incompleto" => EEducationStatus.Incomplete,
        _ => EEducationStatus.Completed
    };

    private static ELanguageLevel LanguageLevel(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "basic" or "basico" or "básico" or "a1" or "a2" => ELanguageLevel.Basic,
        "advanced" or "avancado" or "avançado" or "b2" or "c1" => ELanguageLevel.Advanced,
        "fluent" or "fluente" or "c2" => ELanguageLevel.Fluent,
        "native" or "nativo" or "materno" or "língua materna" or "lingua materna" => ELanguageLevel.Native,
        _ => ELanguageLevel.Intermediate
    };

    private static string Clip(string? value, int maxLength)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }
}
