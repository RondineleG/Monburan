using System.Net;
using System.Text.Json;
using Monburan.Core.DTOs.Response;

namespace Monburan.Web.Services;

/// <summary>
/// Dados estruturados <c>JobPosting</c> (schema.org) da página da vaga, lidos pelo Google Vagas. Descreve só o
/// que a vaga informa: tipo de contratação e período do salário entram quando preenchidos e somem quando não há dado.
/// </summary>
public static class JobStructuredData
{
    // O codificador padrão escapa < > & ', então o JSON é seguro dentro de <script type="application/ld+json">.
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    private static readonly IReadOnlyDictionary<string, string> EmploymentTypes = new Dictionary<string, string>
    {
        ["Clt"] = "FULL_TIME",
        ["Pj"] = "CONTRACTOR",
        ["Internship"] = "INTERN",
        ["Apprenticeship"] = "INTERN",
        ["Temporary"] = "TEMPORARY",
        ["Freelance"] = "CONTRACTOR"
    };

    private static readonly IReadOnlyDictionary<string, string> SalaryUnits = new Dictionary<string, string>
    {
        ["Hour"] = "HOUR",
        ["Day"] = "DAY",
        ["Week"] = "WEEK",
        ["Month"] = "MONTH",
        ["Year"] = "YEAR"
    };

    public static string Build(JobResponse job, string pageUrl)
    {
        // A coluna do país tem tamanho fixo: o valor volta do banco completado com espaços ("BR   ")
        var country = job.Country?.Trim();

        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "JobPosting",
            ["title"] = job.Title,
            ["description"] = HtmlDescription(job),
            ["datePosted"] = Utc(job.CreatedAt).ToString("yyyy-MM-dd"),
            ["url"] = pageUrl,
            ["directApply"] = false,
            ["hiringOrganization"] = Organization(job, pageUrl)
        };

        if (job.ExpirationDate != default)
            data["validThrough"] = job.ExpirationDate.ToString("yyyy-MM-dd");

        if (!string.IsNullOrWhiteSpace(job.ContractType) && EmploymentTypes.TryGetValue(job.ContractType, out var employmentType))
            data["employmentType"] = employmentType;

        if (!string.IsNullOrWhiteSpace(job.SalaryPeriod)
            && SalaryUnits.TryGetValue(job.SalaryPeriod, out var unitText)
            && (job.SalaryMin is not null || job.SalaryMax is not null))
        {
            data["baseSalary"] = Without(new Dictionary<string, object?>
            {
                ["@type"] = "MonetaryAmount",
                ["currency"] = "BRL",
                ["value"] = Without(new Dictionary<string, object?>
                {
                    ["@type"] = "QuantitativeValue",
                    ["minValue"] = job.SalaryMin,
                    ["maxValue"] = job.SalaryMax,
                    ["unitText"] = unitText
                })
            });
        }

        if (job.Type == "Remote")
        {
            data["jobLocationType"] = "TELECOMMUTE";
            data["applicantLocationRequirements"] = new Dictionary<string, object?>
            {
                ["@type"] = "Country",
                ["name"] = country
            };
        }
        else if (!string.IsNullOrWhiteSpace(job.City) || !string.IsNullOrWhiteSpace(job.State))
        {
            data["jobLocation"] = new Dictionary<string, object?>
            {
                ["@type"] = "Place",
                ["address"] = Without(new Dictionary<string, object?>
                {
                    ["@type"] = "PostalAddress",
                    ["addressLocality"] = job.City?.Trim(),
                    ["addressRegion"] = job.State?.Trim(),
                    ["addressCountry"] = country
                })
            };
        }

        return JsonSerializer.Serialize(data, Options);
    }

    private static Dictionary<string, object?> Organization(JobResponse job, string pageUrl) =>
        Without(new Dictionary<string, object?>
        {
            ["@type"] = "Organization",
            ["name"] = string.IsNullOrWhiteSpace(job.AdvertiserName) ? "Empresa confidencial" : job.AdvertiserName,
            ["logo"] = AbsoluteUrl(pageUrl, job.AdvertiserLogoUrl)
        });

    // Google pede a descrição em HTML; o texto da vaga é codificado e as quebras de linha viram <br>.
    private static string HtmlDescription(JobResponse job)
    {
        var parts = new List<string>();
        Add(parts, null, job.Description);
        Add(parts, "Requisitos", job.Requirements);
        Add(parts, "Responsabilidades", job.Responsibilities);
        Add(parts, "Benefícios", job.Benefits);
        return string.Concat(parts);
    }

    private static void Add(List<string> parts, string? heading, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (heading is not null)
            parts.Add($"<h3>{WebUtility.HtmlEncode(heading)}</h3>");
        parts.Add($"<p>{WebUtility.HtmlEncode(text.Trim()).Replace("\r\n", "\n").Replace("\n", "<br>")}</p>");
    }

    private static Dictionary<string, object?> Without(Dictionary<string, object?> values) =>
        values.Where(pair => pair.Value is not (null or "")).ToDictionary(pair => pair.Key, pair => pair.Value);

    private static string? AbsoluteUrl(string pageUrl, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return Uri.TryCreate(new Uri(pageUrl), url, out var absolute) ? absolute.ToString() : null;
    }

    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value
    };
}
