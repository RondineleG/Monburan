using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Monburan.Core.Services;

/// <summary>
/// Leva apelidos e variações de escrita de habilidades ao nome canônico usado na plataforma
/// ("dotnet core" → ".NET", "csharp" → "C#"), para que a mesma habilidade não vire várias
/// habilidades diferentes entre currículos. O que não tem mapeamento conhecido só é limpo
/// (espaços, capitalização) — nunca inventado nem descartado.
/// </summary>
public static class SkillNormalizer
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["c#"] = "C#",
        ["csharp"] = "C#",
        ["c sharp"] = "C#",
        [".net"] = ".NET",
        ["dotnet"] = ".NET",
        [".net core"] = ".NET",
        ["dotnet core"] = ".NET",
        [".net framework"] = ".NET Framework",
        ["dotnet framework"] = ".NET Framework",
        ["aspnet"] = "ASP.NET Core",
        ["asp.net"] = "ASP.NET Core",
        ["asp.net core"] = "ASP.NET Core",
        ["aspnet core"] = "ASP.NET Core",
        ["asp net core"] = "ASP.NET Core",
        ["aspnet mvc"] = "ASP.NET MVC",
        ["asp.net mvc"] = "ASP.NET MVC",
        ["entity framework"] = "Entity Framework",
        ["entity framework core"] = "Entity Framework",
        ["ef"] = "Entity Framework",
        ["ef core"] = "Entity Framework",
        ["sql server"] = "SQL Server",
        ["sqlserver"] = "SQL Server",
        ["ms sql"] = "SQL Server",
        ["mssql"] = "SQL Server",
        ["postgresql"] = "PostgreSQL",
        ["postgres"] = "PostgreSQL",
        ["mysql"] = "MySQL",
        ["mongodb"] = "MongoDB",
        ["mongo"] = "MongoDB",
        ["javascript"] = "JavaScript",
        ["java script"] = "JavaScript",
        ["js"] = "JavaScript",
        ["typescript"] = "TypeScript",
        ["ts"] = "TypeScript",
        ["reactjs"] = "React",
        ["react js"] = "React",
        ["react.js"] = "React",
        ["angularjs"] = "AngularJS",
        ["angular.js"] = "AngularJS",
        ["vuejs"] = "Vue.js",
        ["vue js"] = "Vue.js",
        ["nodejs"] = "Node.js",
        ["node js"] = "Node.js",
        ["node"] = "Node.js",
        ["rabbitmq"] = "RabbitMQ",
        ["rabbit mq"] = "RabbitMQ",
        ["amazon sqs"] = "Amazon SQS",
        ["aws sqs"] = "Amazon SQS",
        ["sqs"] = "Amazon SQS",
        ["amazon sns"] = "Amazon SNS",
        ["sns"] = "Amazon SNS",
        ["amazon s3"] = "Amazon S3",
        ["aws s3"] = "Amazon S3",
        ["s3"] = "Amazon S3",
        ["amazon web services"] = "AWS",
        ["amazon aws"] = "AWS",
        ["google cloud"] = "Google Cloud",
        ["google cloud platform"] = "Google Cloud",
        ["gcp"] = "Google Cloud",
        ["microsoft azure"] = "Azure",
        ["azure devops"] = "Azure DevOps",
        ["rest"] = "APIs RESTful",
        ["restful"] = "APIs RESTful",
        ["rest api"] = "APIs RESTful",
        ["api rest"] = "APIs RESTful",
        ["apis restful"] = "APIs RESTful",
        ["api restful"] = "APIs RESTful",
        ["restful api"] = "APIs RESTful",
        ["microservices"] = "Microsserviços",
        ["micro services"] = "Microsserviços",
        ["microsservicos"] = "Microsserviços",
        ["microsserviços"] = "Microsserviços",
        ["ci/cd"] = "CI/CD",
        ["cicd"] = "CI/CD",
        ["unit tests"] = "Testes unitários",
        ["unit testing"] = "Testes unitários",
        ["testes unitarios"] = "Testes unitários",
        ["testes unitários"] = "Testes unitários",
        ["scrum"] = "Scrum",
        ["kanban"] = "Kanban",
        ["git"] = "Git",
        ["github"] = "GitHub",
        ["git hub"] = "GitHub",
        ["gitlab"] = "GitLab",
        ["docker"] = "Docker",
        ["kubernetes"] = "Kubernetes",
        ["k8s"] = "Kubernetes",
        ["redis"] = "Redis",
        ["kafka"] = "Kafka",
        ["apache kafka"] = "Kafka",
        ["java"] = "Java",
        ["python"] = "Python",
        ["html"] = "HTML",
        ["html5"] = "HTML",
        ["css"] = "CSS",
        ["css3"] = "CSS",
        ["linux"] = "Linux",
        ["windows"] = "Windows",
        ["excel"] = "Excel",
        ["power bi"] = "Power BI",
        ["powerbi"] = "Power BI",
        ["ingles"] = "Inglês",
        ["inglês"] = "Inglês",
        ["espanhol"] = "Espanhol",
    };

    /// <summary>Nome canônico da habilidade, ou o texto limpo quando não há mapeamento.</summary>
    public static string Normalize(string? raw)
    {
        var cleaned = Cleanup(raw);
        if (cleaned.Length == 0)
            return string.Empty;

        if (Aliases.TryGetValue(cleaned.ToLowerInvariant(), out var canonical))
            return canonical;

        // Versões soltas da família .NET convergem para o nome canônico
        if (Regex.IsMatch(cleaned, @"^\.?net\s*(core\s*)?\d+(\.\d+)?$", RegexOptions.IgnoreCase))
            return ".NET";
        if (Regex.IsMatch(cleaned, @"^\.?net\s*framework\s*\d+(\.\d+)?$", RegexOptions.IgnoreCase))
            return ".NET Framework";

        return cleaned;
    }

    /// <summary>
    /// Normaliza uma lista inteira: aplica o mapeamento, remove vazios e junta duplicatas
    /// que só diferem por maiúsculas/acentos. A ordem original é preservada.
    /// </summary>
    public static List<string> NormalizeAll(IEnumerable<string?> skills)
    {
        var result = new List<string>();
        foreach (var raw in skills)
        {
            var normalized = Normalize(raw);
            if (normalized.Length == 0)
                continue;

            if (!result.Any(existing => SameAs(existing, normalized)))
                result.Add(normalized);
        }

        return result;
    }

    /// <summary>Duas habilidades são "a mesma" ignorando caixa e acentos ("CSharp" ≡ "csharp").</summary>
    public static bool SameAs(string first, string second) =>
        string.Equals(WithoutAccents(first), WithoutAccents(second), StringComparison.OrdinalIgnoreCase);

    private static string Cleanup(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        // Lixo comum vindo de listas de PDF: marcadores, pontuação e barras nas pontas
        value = value.Trim('-', '•', '*', '·', ',', ';', '|', '/', ' ');
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string WithoutAccents(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString();
    }
}
