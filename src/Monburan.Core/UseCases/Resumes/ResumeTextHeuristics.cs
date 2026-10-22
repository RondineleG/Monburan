using System.Globalization;
using System.Text.RegularExpressions;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;

namespace Monburan.Core.UseCases.Resumes;

/// <summary>
/// Leitura local do texto extraído do PDF, sem IA: detecta as seções usuais de currículo em
/// português e inglês (resumo, experiência, formação, habilidades, idiomas) e monta o rascunho
/// por regras. Serve como fallback quando a pessoa não autoriza o envio ao provedor ou quando a
/// IA não está configurada — e como base de data em pt-BR para o parser da IA.
/// </summary>
public static partial class ResumeTextHeuristics
{
    private static readonly string[] MonthNames =
        ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    private static readonly string[] MonthAbbreviations =
        ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    private static readonly string[] CurrentMarkers =
        ["atualmente", "atual", "presente", "present", "current", "hoje", "até hoje", "em aberto", "now", "o momento", "o presente"];

    private enum Section { Header, Summary, Experience, Education, Skills, Languages, Other }

    /// <summary>Lê o texto bruto e devolve o rascunho estruturado por regras locais.</summary>
    public static ImportedResumeDraft Parse(string text, string fileName)
    {
        var draft = new ImportedResumeDraft
        {
            ExtractedBy = EResumeImportMode.Local,
            Title = Path.GetFileNameWithoutExtension(fileName)
        };

        var sections = SplitSections(text);
        if (sections.Count <= 1 && sections.Values.Sum(lines => lines.Count) == 0)
            return draft;

        draft.Summary = JoinParagraphs(sections.GetValueOrDefault(Section.Summary));
        if (draft.Summary is { Length: > 2000 })
            draft.Summary = draft.Summary[..2000].TrimEnd() + "…";

        var headerLines = sections.GetValueOrDefault(Section.Header) ?? [];
        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Title == Path.GetFileNameWithoutExtension(fileName))
            draft.Title = GuessTitle(headerLines) ?? draft.Title;

        draft.Experiences = ParseExperiences(sections.GetValueOrDefault(Section.Experience) ?? []);
        draft.Education = ParseEducations(sections.GetValueOrDefault(Section.Education) ?? []);
        draft.Skills = ParseSkills(sections.GetValueOrDefault(Section.Skills) ?? []);
        draft.Languages = ParseLanguages(sections.GetValueOrDefault(Section.Languages) ?? []);

        // "Tecnologias: ..." no fim de cada experiência alimenta as habilidades também
        foreach (var technologies in draft.Experiences
                     .Select(e => e.Description)
                     .Where(d => d is not null)
                     .SelectMany(d => TechnologiesLineRegex().Matches(d!).Cast<Match>())
                     .SelectMany(m => SplitSkills(m.Groups[1].Value)))
        {
            var skill = Services.SkillNormalizer.Normalize(technologies);
            if (skill.Length > 0 && !draft.Skills.Any(existing => Services.SkillNormalizer.SameAs(existing.Skill, skill)))
                draft.Skills.Add(new ImportedSkill { Skill = skill, Level = 3 });
        }

        draft.MissingFields = MissingFieldsOf(draft);
        return draft;
    }

    /// <summary>Data em pt-BR ou formatos comuns: "março de 2026", "mar 2026", "03/2026", "2026-03", "2026".</summary>
    public static DateTime? ParseDate(string value)
    {
        var cleaned = value.Trim().TrimEnd('.', ')').ToLowerInvariant();
        if (cleaned.Length == 0)
            return null;

        if (DateTime.TryParseExact(cleaned, ["yyyy-MM-dd", "yyyy-MM"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return iso;

        var numeric = Regex.Match(cleaned, @"^(\d{1,2})[/\-.](\d{4})$");
        if (numeric.Success && int.TryParse(numeric.Groups[2].Value, out var year) && int.TryParse(numeric.Groups[1].Value, out var monthNum) && monthNum is >= 1 and <= 12)
            return new DateTime(year, monthNum, 1);

        if (Regex.IsMatch(cleaned, @"^(19|20)\d{2}$") && int.TryParse(cleaned, out var onlyYear))
            return new DateTime(onlyYear, 1, 1);

        var monthYear = Regex.Match(cleaned, @"([a-zç]+)\s+(?:de\s+)?(\d{4})");
        if (monthYear.Success)
        {
            var monthIndex = MonthIndex(monthYear.Groups[1].Value);
            if (monthIndex > 0 && int.TryParse(monthYear.Groups[2].Value, out var y))
                return new DateTime(y, monthIndex, 1);
        }

        return null;
    }

    /// <summary>A linha é um intervalo de datas de emprego? Extrai início, fim e "atual".</summary>
    public static bool TryParseDateRange(string line, out DateTime? start, out DateTime? end, out bool isCurrent)
    {
        start = null;
        end = null;
        isCurrent = false;

        var lower = line.ToLowerInvariant();
        var match = DateRangeLineRegex().Match(lower);
        if (!match.Success)
            return false;

        var parts = DateRangeSeparatorRegex().Split(match.Value).Where(p => p.Trim().Length > 0).Select(p => p.Trim()).ToArray();
        if (parts.Length == 0)
            return false;

        start = ParseDate(parts[0]);
        if (start is null)
            return false;

        if (parts.Length > 1)
        {
            isCurrent = IsCurrentMarker(parts[1]);
            if (!isCurrent)
                end = ParseDate(parts[1]);
            else
                end = null;
        }

        return true;
    }

    public static bool IsCurrentMarker(string value) =>
        CurrentMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));

    /// <summary>Campos ainda vazios ou incertos do rascunho, na ordem das perguntas do wizard.</summary>
    public static List<string> MissingFieldsOf(ImportedResumeDraft draft)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.Title))
            missing.Add("title");
        if (string.IsNullOrWhiteSpace(draft.Summary))
            missing.Add("summary");
        if (draft.Experiences.Count == 0)
            missing.Add("experiences");
        else if (draft.Experiences.Any(e => e.StartDate is null))
            missing.Add("experienceDates");
        if (draft.Education.Count == 0)
            missing.Add("education");
        if (draft.Languages.Count == 0)
            missing.Add("languages");
        if (draft.Skills.Count == 0)
            missing.Add("skills");
        return missing;
    }

    private static Dictionary<Section, List<string>> SplitSections(string text)
    {
        var result = new Dictionary<Section, List<string>>();
        var current = Section.Header;
        result[current] = [];

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            var section = SectionOf(line);
            if (section is not null)
            {
                current = section.Value;
                if (!result.ContainsKey(current))
                    result[current] = [];
                continue;
            }

            result[current].Add(line);
        }

        return result;
    }

    private static Section? SectionOf(string line)
    {
        if (line.Length > 60)
            return null;

        var normalized = WithoutAccents(line).Trim().TrimEnd(':', '.', '-');
        if (normalized.Length == 0)
            return null;

        if (MatchesAny(normalized, ["resumo", "resumo profissional", "perfil", "perfil profissional", "objetivo", "objetivo profissional",
            "summary", "professional summary", "objective", "career objective", "sobre mim", "about"]))
            return Section.Summary;
        if (MatchesAny(normalized, ["experiencia", "experiencia profissional", "experiencias", "experiencias profissionais",
            "experience", "work experience", "professional experience", "employment history", "historico profissional",
            "atuacao profissional", "carreira"]))
            return Section.Experience;
        if (MatchesAny(normalized, ["formacao", "formacao academica", "educacao", "education", "academic background",
            "escolaridade", "formacao academica e cursos"]))
            return Section.Education;
        if (MatchesAny(normalized, ["habilidades", "competencias", "principais competencias", "skills", "technical skills",
            "conhecimentos", "conhecimentos tecnicos", "tecnologias", "hard skills", "core skills", "qualificacoes"]))
            return Section.Skills;
        if (MatchesAny(normalized, ["idiomas", "languages", "language"]))
            return Section.Languages;
        if (MatchesAny(normalized, ["certificacoes", "certificados", "cursos", "certifications", "certificates",
            "courses", "licencas", "licenses", "projetos", "projects", "publicacoes", "premios", "awards",
            "informacoes adicionais", "additional information", "referencias", "references"]))
            return Section.Other;

        return null;
    }

    private static bool MatchesAny(string normalized, string[] candidates) =>
        candidates.Any(c => string.Equals(normalized, c, StringComparison.OrdinalIgnoreCase));

    private static string? GuessTitle(List<string> headerLines)
    {
        foreach (var line in headerLines.Skip(1).Take(4))
        {
            if (line.Contains('@') || PhoneLikeRegex().IsMatch(line))
                continue;
            if (line.Contains('|'))
                return line.Split('|')[0].Trim();
            if (line.Length is > 4 and < 80 && !line.Contains("http", StringComparison.OrdinalIgnoreCase))
                return line.Trim();
        }

        return null;
    }

    private static List<ImportedExperience> ParseExperiences(List<string> lines) =>
        SplitBlocks(lines)
            .Select(ParseExperienceBlock)
            .Where(e => e is not null)
            .Cast<ImportedExperience>()
            .ToList();

    /// <summary>
    /// Quebra a seção em blocos por linha de intervalo de datas: o cabeçalho do bloco são as linhas
    /// curtas (empresa/cargo) imediatamente antes da data; o corpo vai até o cabeçalho do bloco
    /// seguinte. Blocos sem data são devolvidos como estão.
    /// </summary>
    private static List<List<string>> SplitBlocks(List<string> lines)
    {
        var dateIndexes = lines
            .Select((line, index) => (line, index))
            .Where(x => TryParseDateRange(x.line, out _, out _, out _))
            .Select(x => x.index)
            .ToList();

        if (dateIndexes.Count == 0)
            return [lines];

        var blocks = new List<List<string>>();
        var previousEnd = 0;
        for (var i = 0; i < dateIndexes.Count; i++)
        {
            var dateIndex = dateIndexes[i];
            var headerStart = HeaderStart(lines, previousEnd, dateIndex);
            var bodyEnd = i + 1 < dateIndexes.Count ? HeaderStart(lines, dateIndexes[i] + 1, dateIndexes[i + 1]) : lines.Count;

            var block = new List<string>();
            block.AddRange(lines.Skip(headerStart).Take(dateIndex - headerStart));
            block.Add(lines[dateIndex]);
            block.AddRange(lines.Skip(dateIndex + 1).Take(Math.Max(0, bodyEnd - dateIndex - 1)));
            blocks.Add(block);
            previousEnd = dateIndex + 1;
        }

        return blocks;
    }

    /// <summary>
    /// Índice onde começa o cabeçalho de um bloco que termina na linha de data <paramref name="dateIndex"/>:
    /// anda para trás enquanto a linha for curta e não for continuação de parágrafo nem rótulo de seção.
    /// </summary>
    private static int HeaderStart(List<string> lines, int from, int dateIndex)
    {
        var start = dateIndex;
        for (var i = dateIndex - 1; i >= from; i--)
        {
            var line = lines[i];
            var shortish = line.Length <= 80 && !line.EndsWith('.') && !line.EndsWith(',') && !TechnologiesLineRegex().IsMatch(line);
            if (!shortish)
                break;
            start = i;
        }

        return start;
    }

    private static ImportedExperience? ParseExperienceBlock(List<string> block)
    {
        var dateIndex = block.FindIndex(line => TryParseDateRange(line, out _, out _, out _));
        if (dateIndex < 0)
            return null;

        TryParseDateRange(block[dateIndex], out var start, out var end, out var isCurrent);

        var before = block.Take(dateIndex).Where(l => l.Length > 0).ToList();
        var after = block.Skip(dateIndex + 1).Where(l => l.Length > 0).ToList();

        var company = before.LastOrDefault() ?? string.Empty;
        var position = after.FirstOrDefault(l => !LooksLikeLocation(l)) ?? string.Empty;
        if (after.Count > 0 && after[0] == position)
            after.RemoveAt(0);

        // Cabeçalho em linha única "Cargo | Empresa" antes da data
        if (before.Count > 0 && company.Contains(" - ") && position.Length == 0)
        {
            var parts = company.Split(" - ", 2, StringSplitOptions.TrimEntries);
            position = parts[0];
            company = parts.Length > 1 ? parts[1] : string.Empty;
        }

        // Quando a linha anterior tem pipes (lista de tecnologias) e a posterior não, provavelmente
        // está invertido: anterior é cargo, posterior é empresa
        if (company.Contains('|') && position.Length > 0 && !position.Contains('|'))
            (company, position) = (position, company);

        var description = JoinParagraphs(after.Where(l => !LooksLikeLocation(l)).ToList());

        if (company.Length == 0 && position.Length == 0)
            return null;

        return new ImportedExperience
        {
            Company = company,
            Position = position,
            Description = description,
            StartDate = start,
            EndDate = end,
            IsCurrent = isCurrent
        };
    }

    private static List<ImportedEducation> ParseEducations(List<string> lines) =>
        SplitBlocks(lines)
            .Select(ParseEducationBlock)
            .Where(e => e is not null)
            .Cast<ImportedEducation>()
            .ToList();

    private static ImportedEducation? ParseEducationBlock(List<string> block)
    {
        if (block.Count == 0)
            return null;

        var dateIndex = block.FindIndex(line => TryParseDateRange(line, out _, out _, out _));
        DateTime? start = null, end = null;
        var isCurrent = false;
        if (dateIndex >= 0)
            TryParseDateRange(block[dateIndex], out start, out end, out isCurrent);

        var infoLines = dateIndex >= 0 ? block.Take(dateIndex).Concat(block.Skip(dateIndex + 1)).ToList() : block;

        var institution = infoLines.FirstOrDefault(l => InstitutionRegex().IsMatch(l))
                          ?? infoLines.FirstOrDefault()
                          ?? string.Empty;
        var course = infoLines.FirstOrDefault(l => l != institution && (DegreeRegex().IsMatch(l) || !InstitutionRegex().IsMatch(l)))
                     ?? string.Empty;

        if (institution.Length == 0 && course.Length == 0)
            return null;

        var text = string.Join(' ', infoLines).ToLowerInvariant();
        var status = isCurrent || text.Contains("cursando") || text.Contains("andamento") || text.Contains("previs") || (end is { } e && e > DateTime.Today)
            ? EEducationStatus.Ongoing
            : text.Contains("trancad") || text.Contains("interrompid") ? EEducationStatus.Dropped
            : text.Contains("incomplet") ? EEducationStatus.Incomplete
            : EEducationStatus.Completed;

        return new ImportedEducation
        {
            Institution = institution.Replace('|', '—').Trim('—', ' ', '|'),
            Course = course,
            Level = LevelFromText(text),
            Status = status,
            StartDate = start,
            EndDate = status == EEducationStatus.Ongoing ? end : end
        };
    }

    private static EEducationLevel LevelFromText(string text) =>
        text switch
        {
            _ when text.Contains("doutorad") || text.Contains("phd") => EEducationLevel.Doctorate,
            _ when text.Contains("mestrado") || text.Contains("msc") => EEducationLevel.Master,
            _ when text.Contains("pós-gradu") || text.Contains("pos-gradu") || text.Contains("mba") || text.Contains("especializa") => EEducationLevel.Postgraduate,
            _ when text.Contains("tecnólog") || text.Contains("tecnolog") || text.Contains("cst") || text.Contains("técnico") || text.Contains("tecnico") => EEducationLevel.Technical,
            _ when text.Contains("bacharel") || text.Contains("licenciat") || text.Contains("graduaç") || text.Contains("graduac") || text.Contains("superior") || text.Contains("bachelor") || text.Contains("degree") => EEducationLevel.Bachelor,
            _ when text.Contains("ensino médio") || text.Contains("ensino medio") || text.Contains("high school") => EEducationLevel.HighSchool,
            _ => EEducationLevel.Bachelor
        };

    private static List<ImportedSkill> ParseSkills(List<string> lines)
    {
        var skills = new List<ImportedSkill>();
        foreach (var raw in lines.SelectMany(SplitSkills))
        {
            var skill = Services.SkillNormalizer.Normalize(raw);
            if (skill.Length > 0 && !skills.Any(existing => Services.SkillNormalizer.SameAs(existing.Skill, skill)))
                skills.Add(new ImportedSkill { Skill = skill, Level = 3 });
        }

        return skills;
    }

    /// <summary>Separa uma linha de habilidades: "C# | .NET, Docker" vira itens.</summary>
    public static IEnumerable<string> SplitSkills(string line) =>
        SkillSeparatorRegex().Split(line)
            .Select(token => token.Trim('-', '•', '*', '·', ' '))
            .Where(token => token.Length >= 2 && token.Any(c => char.IsLetter(c) || c == '#' || c == '+'));

    private static List<ImportedLanguage> ParseLanguages(List<string> lines)
    {
        var languages = new List<ImportedLanguage>();
        foreach (var token in lines.SelectMany(line => SkillSeparatorRegex().Split(line)).Where(t => t.Trim().Length > 0).Select(t => t.Trim()))
        {
            var match = LanguageLevelRegex().Match(token);
            var name = match.Success ? token[..match.Index].Trim(' ', '-', ':', '–', '(', ')') : token.Trim(' ', '-', ':', '–');
            var level = match.Success ? LevelOf(match.Value) : ELanguageLevel.Intermediate;

            var normalized = Services.SkillNormalizer.Normalize(name);
            if (normalized.Length > 0 && normalized.Length <= 40
                && !languages.Any(existing => Services.SkillNormalizer.SameAs(existing.Language, normalized)))
                languages.Add(new ImportedLanguage { Language = normalized, Level = level });
        }

        return languages;
    }

    private static ELanguageLevel LevelOf(string marker) => marker.ToLowerInvariant() switch
    {
        var m when m.Contains("nativ") || m.Contains("materna") => ELanguageLevel.Native,
        var m when m.Contains("fluent") => ELanguageLevel.Fluent,
        var m when m.Contains("avanç") || m.Contains("avanc") || m.Contains("advanced") => ELanguageLevel.Advanced,
        var m when m.Contains("básic") || m.Contains("basic") => ELanguageLevel.Basic,
        _ => ELanguageLevel.Intermediate
    };

    private static bool LooksLikeLocation(string line)
    {
        var lower = WithoutAccents(line).ToLowerInvariant();
        return lower.Contains("brasil") || lower.Contains("brazil") || lower.Contains("remoto") || lower.Contains("remote")
            || lower.Contains("hibrido") || lower.Contains("hybrid") || lower.Contains("presencial")
            || CityStateRegex().IsMatch(lower) && lower.Length < 60;
    }

    private static int MonthIndex(string name)
    {
        // "marco" (sem cedilha) é o mesmo março
        var canonical = string.Equals(name, "marco", StringComparison.OrdinalIgnoreCase) ? "março" : name;

        for (var i = 0; i < MonthNames.Length; i++)
        {
            if (string.Equals(canonical, MonthNames[i], StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        for (var i = 0; i < MonthAbbreviations.Length; i++)
        {
            if (canonical.StartsWith(MonthAbbreviations[i], StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return 0;
    }

    private static string JoinParagraphs(IEnumerable<string>? lines)
    {
        var text = string.Join('\n', (lines ?? []).Where(l => l.Length > 0)).Trim();
        return text.Length == 0 ? string.Empty : text;
    }

    private static string WithoutAccents(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().ToLowerInvariant();
    }

    [GeneratedRegex(@"\b(janeiro|fevereiro|março|marco|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro|jan|fev|mar|abr|mai|jun|jul|ago|set|out|nov|dez)\.?\s*(de\s+)?(19|20)\d{2}\s*([-–—aàem]+\s*)?((janeiro|fevereiro|março|marco|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro|jan|fev|mar|abr|mai|jun|jul|ago|set|out|nov|dez)\.?\s*(de\s+)?(19|20)\d{2}|atualmente|atual|presente|present|current|hoje|ate hoje|em aberto|now)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DateRangeLineRegex();

    [GeneratedRegex(@"[-–—]+|\ba\b|\bà\b|\bto\b|\baté\b|\buntil\b|\bem\b")]
    private static partial Regex DateRangeSeparatorRegex();

    [GeneratedRegex(@"tecnologias?\s*[:：]\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex TechnologiesLineRegex();

    [GeneratedRegex(@"\(?\d{2,3}\)?\s*\d{4,5}[-\s]?\d{4}")]
    private static partial Regex PhoneLikeRegex();

    [GeneratedRegex(@"(universidade|universidade|faculdade|instituto|centro universit[aá]rio|college|university|school|univali|uninter|est[aá]cio|anhanguera|unip|fiap|puc|ufsc|usp|unicesumar)", RegexOptions.IgnoreCase)]
    private static partial Regex InstitutionRegex();

    [GeneratedRegex(@"(bacharel|licenciat|tecn[oó]log|cst|t[eé]cnico|mestrado|doutorado|p[oó]s|mba|gradua|degree|major|curso|an[aá]lise|engenharia|ci[eê]ncia|administra)", RegexOptions.IgnoreCase)]
    private static partial Regex DegreeRegex();

    [GeneratedRegex(@"\s*[|·•;,/]\s*")]
    private static partial Regex SkillSeparatorRegex();

    [GeneratedRegex(@",\s*[A-Z]{2}\b")]
    private static partial Regex CityStateRegex();

    [GeneratedRegex(@"(nativo|materna|fluente|avan[çc]ado|intermedi[áa]rio|b[áa]sico|iniciante|basic|intermediate|advanced|fluent|native|a1|a2|b1|b2|c1|c2)")]
    private static partial Regex LanguageLevelRegex();
}
