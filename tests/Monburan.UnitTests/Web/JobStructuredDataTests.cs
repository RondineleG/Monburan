using System.Text.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Response;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class JobStructuredDataTests
{
    private const string PageUrl = "https://monburan.com.br/jobs/6f1d6c0e-5f0a-4a52-9d4b-0d6f3c1a9e11";

    private static JobResponse Job(Action<JobResponse>? customize = null)
    {
        var job = new JobResponse
        {
            Id = Guid.Parse("6f1d6c0e-5f0a-4a52-9d4b-0d6f3c1a9e11"),
            Title = "Desenvolvedor .NET",
            Description = "Atuar em produtos digitais.\nTrabalho em equipe.",
            Requirements = "C# e SQL",
            AdvertiserName = "Tech Solutions",
            City = "São Paulo",
            State = "SP",
            Country = "BR",
            Type = "Hybrid",
            CreatedAt = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc),
            ExpirationDate = new DateTime(2026, 11, 16),
            SalaryMin = 8000,
            SalaryMax = 12000,
            SalaryDisplay = true
        };
        customize?.Invoke(job);
        return job;
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Build_DescribesAJobPosting()
    {
        var data = Parse(JobStructuredData.Build(Job(), PageUrl));

        data.GetProperty("@context").GetString().Should().Be("https://schema.org");
        data.GetProperty("@type").GetString().Should().Be("JobPosting");
        data.GetProperty("title").GetString().Should().Be("Desenvolvedor .NET");
        data.GetProperty("datePosted").GetString().Should().Be("2026-10-17");
        data.GetProperty("validThrough").GetString().Should().Be("2026-11-16");
        data.GetProperty("url").GetString().Should().Be(PageUrl);
        data.GetProperty("hiringOrganization").GetProperty("name").GetString().Should().Be("Tech Solutions");
    }

    [Fact]
    public void Build_UsesAnAddress_ForOnSiteAndHybridJobs()
    {
        var data = Parse(JobStructuredData.Build(Job(), PageUrl));

        var address = data.GetProperty("jobLocation").GetProperty("address");
        address.GetProperty("addressLocality").GetString().Should().Be("São Paulo");
        address.GetProperty("addressRegion").GetString().Should().Be("SP");
        address.GetProperty("addressCountry").GetString().Should().Be("BR");
        data.TryGetProperty("jobLocationType", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_TrimsTheCountryReturnedByTheFixedSizeColumn()
    {
        var onSite = Parse(JobStructuredData.Build(Job(job => job.Country = "BR   "), PageUrl));
        onSite.GetProperty("jobLocation").GetProperty("address").GetProperty("addressCountry").GetString().Should().Be("BR");

        var remote = Parse(JobStructuredData.Build(Job(job => { job.Country = "BR   "; job.Type = "Remote"; }), PageUrl));
        remote.GetProperty("applicantLocationRequirements").GetProperty("name").GetString().Should().Be("BR");
    }

    [Fact]
    public void Build_MarksRemoteJobsAsTelecommute()
    {
        var data = Parse(JobStructuredData.Build(Job(job => job.Type = "Remote"), PageUrl));

        data.GetProperty("jobLocationType").GetString().Should().Be("TELECOMMUTE");
        data.GetProperty("applicantLocationRequirements").GetProperty("name").GetString().Should().Be("BR");
        data.TryGetProperty("jobLocation", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_DoesNotClaimWhatTheJobDoesNotInform()
    {
        var data = Parse(JobStructuredData.Build(Job(), PageUrl));

        // sem tipo de contratação nem período do salário no cadastro, o JSON-LD omite os campos
        data.TryGetProperty("baseSalary", out _).Should().BeFalse();
        data.TryGetProperty("employmentType", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("Clt", "FULL_TIME")]
    [InlineData("Pj", "CONTRACTOR")]
    [InlineData("Internship", "INTERN")]
    [InlineData("Apprenticeship", "INTERN")]
    [InlineData("Temporary", "TEMPORARY")]
    [InlineData("Freelance", "CONTRACTOR")]
    public void Build_MapsTheContractTypeToEmploymentType(string contractType, string employmentType)
    {
        var data = Parse(JobStructuredData.Build(Job(job => job.ContractType = contractType), PageUrl));

        data.GetProperty("employmentType").GetString().Should().Be(employmentType);
    }

    [Theory]
    [InlineData("Hour", "HOUR")]
    [InlineData("Day", "DAY")]
    [InlineData("Week", "WEEK")]
    [InlineData("Month", "MONTH")]
    [InlineData("Year", "YEAR")]
    public void Build_IncludesBaseSalaryWithTheUnitText_WhenThePeriodIsInformed(string salaryPeriod, string unitText)
    {
        var data = Parse(JobStructuredData.Build(Job(job => job.SalaryPeriod = salaryPeriod), PageUrl));

        var value = data.GetProperty("baseSalary");
        value.GetProperty("currency").GetString().Should().Be("BRL");
        value.GetProperty("value").GetProperty("minValue").GetDecimal().Should().Be(8000);
        value.GetProperty("value").GetProperty("maxValue").GetDecimal().Should().Be(12000);
        value.GetProperty("value").GetProperty("unitText").GetString().Should().Be(unitText);
    }

    [Fact]
    public void Build_OmitsBaseSalary_WhenThereIsNoSalaryValue()
    {
        var data = Parse(JobStructuredData.Build(Job(job =>
        {
            job.SalaryPeriod = "Month";
            job.SalaryMin = null;
            job.SalaryMax = null;
        }), PageUrl));

        data.TryGetProperty("baseSalary", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_OmitsBaseSalary_WhenThePeriodIsNotInformed()
    {
        var data = Parse(JobStructuredData.Build(Job(), PageUrl));

        data.TryGetProperty("baseSalary", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_DescriptionIsEscapedHtml_WithLineBreaks()
    {
        var json = JobStructuredData.Build(Job(job => job.Description = "Linha 1\nLinha 2 <b>forte</b> & mais"), PageUrl);

        var description = Parse(json).GetProperty("description").GetString()!;
        description.Should().Contain("Linha 1<br>Linha 2 &lt;b&gt;forte&lt;/b&gt; &amp; mais");
        description.Should().Contain("<h3>Requisitos</h3><p>C# e SQL</p>");
    }

    [Fact]
    public void Build_CannotBreakOutOfTheScriptTag()
    {
        var json = JobStructuredData.Build(
            Job(job =>
            {
                job.Title = "</script><script>alert(1)</script>";
                job.AdvertiserName = "<img src=x onerror=alert(1)>";
            }),
            PageUrl);

        json.Should().NotContain("<");
        json.Should().NotContain(">");
        Parse(json).GetProperty("title").GetString().Should().Be("</script><script>alert(1)</script>");
    }

    [Fact]
    public void Build_MakesTheLogoUrlAbsolute_AndNamesAnonymousCompanies()
    {
        var data = Parse(JobStructuredData.Build(
            Job(job =>
            {
                job.AdvertiserName = null;
                job.AdvertiserLogoUrl = "/uploads/logo.png";
            }),
            PageUrl));

        var organization = data.GetProperty("hiringOrganization");
        organization.GetProperty("name").GetString().Should().Be("Empresa confidencial");
        organization.GetProperty("logo").GetString().Should().Be("https://monburan.com.br/uploads/logo.png");
    }
}
