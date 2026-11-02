using FluentAssertions;
using Monburan.Core.DTOs.Response;
using Monburan.Web.Services;
using Xunit;

namespace Monburan.UnitTests.Web;

public class JobDisplayLabelsTests
{
    [Fact]
    public void Salary_IncludesThePeriodInEveryScreen()
    {
        var job = new JobResponse { SalaryMin = 8000, SalaryMax = 12000, SalaryCurrency = "BRL", SalaryDisplay = true, SalaryPeriod = "Month" };

        JobDisplay.Salary(job).Should().Be("R$ 8.000 – 12.000 por mês");
    }

    [Fact]
    public void Salary_WithoutPeriodKeepsTheRangeAlone()
    {
        var job = new JobResponse { SalaryMin = 8000, SalaryCurrency = "BRL", SalaryDisplay = true };

        JobDisplay.Salary(job).Should().Be("A partir de R$ 8.000");
    }

    [Fact]
    public void Salary_HiddenOrAbsent_ReturnsNull()
    {
        JobDisplay.Salary(new JobResponse { SalaryMin = 8000, SalaryDisplay = false }).Should().BeNull();
        JobDisplay.Salary(new JobResponse { SalaryDisplay = true }).Should().BeNull("salário não informado nunca é estimado");
    }

    [Theory]
    [InlineData("social-media", "Social media")]
    [InlineData("ads", "Ads")]
    [InlineData("SEO", "SEO")]
    [InlineData(".NET", ".NET")]
    [InlineData("conteudo_criativo", "Conteudo criativo")]
    [InlineData("C#", "C#")]
    public void TagLabel_IsReadable(string raw, string expected)
    {
        JobDisplay.TagLabel(raw).Should().Be(expected);
    }

    [Fact]
    public void Tags_ShowReadableLabelsAndSkipTheCategory()
    {
        var tags = JobDisplay.Tags("social-media,ads,Marketing", max: 5, exclude: "Marketing");

        tags.Should().Equal("Social media", "Ads");
    }
}
