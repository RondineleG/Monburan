using System.Text.Json;

using Monburan.Core.DTOs.Response;

namespace Monburan.Core.UseCases.Resumes;

/// <summary>
/// Lê a resposta da IA com tolerância: aceita o JSON puro, dentro de bloco de código ou cercado de texto, nomes em
/// camelCase ou snake_case, listas no lugar de textos e vice-versa. O que sobra é limpo e limitado em tamanho, já
/// que o conteúdo vem de um modelo e vai para a tela.
/// </summary>
public static class ResumeImprovementParser
{
    private const int MaxSummaryLength = 1500;
    private const int MaxItemLength = 500;
    private const int MaxItems = 8;
    private const int MaxExperiences = 15;

    public static bool TryParse(string? content, out ResumeImprovementResponse? response)
    {
        response = null;

        var json = ExtractJson(content);
        if (json is null)
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var parsed = new ResumeImprovementResponse
            {
                Summary = Clip(Text(Find(root, "summary")), MaxSummaryLength),
                Experiences = Experiences(Find(root, "experiences")),
                SkillsToAdd = Items(Find(root, "skillsToAdd")),
                MissingSections = Items(Find(root, "missingSections")),
                Tips = Items(Find(root, "tips"))
            };

            var hasContent = parsed.Summary.Length > 0
                || parsed.Experiences.Count > 0
                || parsed.SkillsToAdd.Count > 0
                || parsed.MissingSections.Count > 0
                || parsed.Tips.Count > 0;

            if (!hasContent)
                return false;

            response = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ExtractJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        // Tira cercas de código (```json ... ```) e texto solto antes ou depois do objeto
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        return start >= 0 && end > start ? content[start..(end + 1)] : null;
    }

    private static JsonElement? Find(JsonElement element, string name)
    {
        var wanted = Normalize(name);
        foreach (var property in element.EnumerateObject())
        {
            if (Normalize(property.Name) == wanted)
                return property.Value;
        }

        return null;
    }

    private static string Normalize(string name) =>
        name.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();

    private static List<ExperienceImprovement> Experiences(JsonElement? element)
    {
        var result = new List<ExperienceImprovement>();
        if (element is not { ValueKind: JsonValueKind.Array } array)
            return result;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var suggestions = Items(Find(item, "suggestions"));
            if (suggestions.Count == 0)
                continue;

            result.Add(new ExperienceImprovement
            {
                Company = Clip(Text(Find(item, "company")), 150),
                Position = Clip(Text(Find(item, "position")), 150),
                Suggestions = suggestions
            });

            if (result.Count == MaxExperiences)
                break;
        }

        return result;
    }

    private static List<string> Items(JsonElement? element)
    {
        var result = new List<string>();

        switch (element)
        {
            case { ValueKind: JsonValueKind.Array } array:
                foreach (var item in array.EnumerateArray())
                    Add(result, Text(item));
                break;
            case { ValueKind: JsonValueKind.String } text:
                // Um texto único no lugar de uma lista: cada linha vira um item
                foreach (var line in (text.GetString() ?? string.Empty).Split('\n'))
                    Add(result, line.TrimStart('-', '*', '•', ' '));
                break;
        }

        return result;
    }

    private static void Add(List<string> items, string? value)
    {
        var clipped = Clip(value, MaxItemLength);
        if (clipped.Length > 0 && items.Count < MaxItems)
            items.Add(clipped);
    }

    private static string? Text(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.String } text => text.GetString(),
        { ValueKind: JsonValueKind.Number } number => number.ToString(),
        _ => null
    };

    private static string Clip(string? value, int maxLength)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }
}
