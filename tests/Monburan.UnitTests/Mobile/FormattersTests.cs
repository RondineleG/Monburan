using FluentAssertions;

using Monburan.Core.DTOs.Response;
using Monburan.Mobile.Shared.Ui;

namespace Monburan.UnitTests.Mobile;

public class FormattersTests
{
    [Fact]
    public void Salary_IsHidden_WhenAdvertiserOptedOut()
    {
        var job = new JobResponse { SalaryMin = 5000, SalaryMax = 7000, SalaryDisplay = false };

        Formatters.Salary(job).Should().BeNull();
    }

    [Theory]
    [InlineData("BRL", "R$")]
    [InlineData("USD", "US$")]
    [InlineData("EUR", "€")]
    public void Salary_UsesTheJobCurrency(string currency, string symbol)
    {
        var job = new JobResponse { SalaryMin = 5000, SalaryMax = 7000, SalaryDisplay = true, SalaryCurrency = currency };

        Formatters.Salary(job).Should().StartWith(symbol);
    }

    [Fact]
    public void Salary_HandlesOpenRanges()
    {
        Formatters.Salary(new JobResponse { SalaryMin = 3000, SalaryDisplay = true }).Should().StartWith("a partir de");
        Formatters.Salary(new JobResponse { SalaryMax = 3000, SalaryDisplay = true }).Should().StartWith("até");
        Formatters.Salary(new JobResponse { SalaryDisplay = true }).Should().BeNull();
    }

    [Theory]
    [InlineData("Sent", "Enviada")]
    [InlineData("Selected", "Selecionada")]
    [InlineData("Rejected", "Não selecionada")]
    [InlineData("Archived", "Arquivada")]
    [InlineData("Desconhecido", "Em análise")]
    public void ApplicationStatus_IsAlwaysPortuguese(string status, string label)
    {
        Formatters.ApplicationStatus(status).Should().Be(label);
    }

    [Fact]
    public void CalendarDate_IsNotShiftedByTimeZone()
    {
        var expiration = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);

        Formatters.CalendarDate(expiration).Should().Be("16/11/2026");
    }
}
