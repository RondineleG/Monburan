using FluentAssertions;

using Monburan.Core.DTOs.Response;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class JobDisplayTests
{
    // 18/10/2026 15:00 UTC = 12:00 em Brasília
    private static readonly DateTime Now = new(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Salary_ShowsTheRange_WithPortugueseThousands()
    {
        var job = new JobResponse { SalaryMin = 8000, SalaryMax = 12000, SalaryDisplay = true };

        JobDisplay.Salary(job).Should().Be("R$ 8.000 – 12.000");
    }

    [Fact]
    public void Salary_ShowsOneValue_WhenMinAndMaxAreEqual()
    {
        var job = new JobResponse { SalaryMin = 5000, SalaryMax = 5000, SalaryDisplay = true };

        JobDisplay.Salary(job).Should().Be("R$ 5.000");
    }

    [Fact]
    public void Salary_HandlesOpenRanges_AndKeepsCentsOnlyWhenNeeded()
    {
        JobDisplay.Salary(new JobResponse { SalaryMin = 3000, SalaryDisplay = true }).Should().Be("A partir de R$ 3.000");
        JobDisplay.Salary(new JobResponse { SalaryMax = 4500.5m, SalaryDisplay = true }).Should().Be("Até R$ 4.500,50");
    }

    [Fact]
    public void Salary_IsNull_WhenHiddenOrMissing()
    {
        JobDisplay.Salary(new JobResponse { SalaryMin = 5000, SalaryMax = 7000, SalaryDisplay = false }).Should().BeNull();
        JobDisplay.Salary(new JobResponse { SalaryDisplay = true }).Should().BeNull();
    }

    [Theory]
    [InlineData("USD", "US$ 1.000 – 2.000")]
    [InlineData("EUR", "€ 1.000 – 2.000")]
    [InlineData("BRL", "R$ 1.000 – 2.000")]
    public void Salary_UsesTheCurrencyOfTheJob(string currency, string expected)
    {
        var job = new JobResponse { SalaryMin = 1000, SalaryMax = 2000, SalaryDisplay = true, SalaryCurrency = currency };

        JobDisplay.Salary(job).Should().Be(expected);
    }

    [Theory]
    [InlineData("São Paulo", "SP", false, "Remote", "São Paulo, SP")]
    [InlineData("Campinas", null, false, "OnSite", "Campinas")]
    [InlineData(null, "RJ", false, "Hybrid", "RJ")]
    [InlineData(null, null, true, "Remote", "Brasil")]
    [InlineData("  ", " ", false, "OnSite", "Local a combinar")]
    public void Location_FallsBackToSomethingReadable(string? city, string? state, bool remote, string type, string expected)
    {
        var job = new JobResponse { City = city, State = state, IsRemote = remote, Type = type };

        JobDisplay.Location(job).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-10-18T09:00:00Z", "hoje")]
    [InlineData("2026-10-17T20:00:00Z", "ontem")]
    [InlineData("2026-10-15T12:00:00Z", "há 3 dias")]
    [InlineData("2026-10-11T12:00:00Z", "há 1 semana")]
    [InlineData("2026-10-04T12:00:00Z", "há 2 semanas")]
    [InlineData("2026-09-18T12:00:00Z", "há 1 mês")]
    [InlineData("2026-08-13T12:00:00Z", "há 2 meses")]
    [InlineData("2025-08-01T12:00:00Z", "há mais de 1 ano")]
    public void Age_CountsCalendarDaysInBrasilia(string createdAtUtc, string expected)
    {
        var createdAt = DateTime.Parse(createdAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        JobDisplay.Age(createdAt, Now).Should().Be(expected);
    }

    [Fact]
    public void Age_UsesTheBrasiliaDay_NotTheUtcDay()
    {
        // 01:30 UTC de 18/10 ainda é 22:30 de 17/10 em Brasília: "ontem" para quem vê às 12:00 do dia 18
        var createdAt = new DateTime(2026, 10, 18, 1, 30, 0, DateTimeKind.Utc);

        JobDisplay.Age(createdAt, Now).Should().Be("ontem");
    }

    [Theory]
    [InlineData("2026-10-18T09:00:00Z", true)]
    [InlineData("2026-10-16T09:00:00Z", true)]
    [InlineData("2026-10-15T09:00:00Z", false)]
    public void IsNew_CoversTheLastThreeDays(string createdAtUtc, bool expected)
    {
        var createdAt = DateTime.Parse(createdAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        JobDisplay.IsNew(createdAt, Now).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-10-18", "Encerra hoje")]
    [InlineData("2026-10-19", "Encerra amanhã")]
    [InlineData("2026-10-22", "Encerra em 4 dias")]
    [InlineData("2026-10-25", "Encerra em 7 dias")]
    [InlineData("2026-10-26", null)]
    [InlineData("2026-10-17", null)]
    public void ClosingNotice_AppearsOnlyInTheLastWeek(string expiration, string? expected)
    {
        JobDisplay.ClosingNotice(DateTime.Parse(expiration), Now).Should().Be(expected);
    }

    [Fact]
    public void ClosingNotice_IsNull_WithoutExpirationDate()
    {
        JobDisplay.ClosingNotice(default, Now).Should().BeNull();
    }

    [Fact]
    public void Tags_AreTrimmedDeduplicatedAndLimited()
    {
        JobDisplay.Tags("C#, .NET, c#, , React ,SQL").Should().Equal("C#", ".NET", "React", "SQL");
        JobDisplay.Tags("a,b,c,d", 2).Should().Equal("a", "b");
        JobDisplay.Tags(null).Should().BeEmpty();
    }

    [Fact]
    public void Excerpt_CollapsesWhitespace_AndCutsAtAWordBoundary()
    {
        JobDisplay.Excerpt("Linha um\n\nlinha   dois", 100).Should().Be("Linha um linha dois");

        var cut = JobDisplay.Excerpt("Buscamos pessoa desenvolvedora para atuar em produtos digitais de grande escala", 40);
        cut.Should().EndWith("…");
        cut.Should().Be("Buscamos pessoa desenvolvedora para…");
    }

    [Fact]
    public void Excerpt_IsEmpty_ForBlankText()
    {
        JobDisplay.Excerpt("  \n ", 50).Should().BeEmpty();
        JobDisplay.Excerpt(null, 50).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Tech Solutions", "T")]
    [InlineData("  marketing digital", "M")]
    [InlineData(null, "M")]
    [InlineData("  ", "M")]
    public void Initial_UsesTheCompanyName(string? name, string expected)
    {
        JobDisplay.Initial(name).Should().Be(expected);
    }

    [Fact]
    public void WhatsAppUrl_EncodesTitleAndLink()
    {
        var url = JobDisplay.WhatsAppUrl("Dev .NET & React", "https://monburan.com.br/jobs/abc");

        url.Should().StartWith("https://wa.me/?text=");
        url.Should().Contain("Dev%20.NET%20%26%20React");
        url.Should().Contain("https%3A%2F%2Fmonburan.com.br%2Fjobs%2Fabc");
    }

    [Theory]
    [InlineData("Remote", "home")]
    [InlineData("Hybrid", "shuffle")]
    [InlineData("OnSite", "building")]
    [InlineData(null, "building")]
    public void ModeIcon_FollowsTheWorkMode(string? type, string expected)
    {
        JobDisplay.ModeIcon(type).Should().Be(expected);
    }
}
