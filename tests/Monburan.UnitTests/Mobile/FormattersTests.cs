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
    [InlineData("GBP", "£")]
    [InlineData("CAD", "CAD")] // moeda sem símbolo conhecido aparece pelo código, nunca como R$
    [InlineData("usd", "US$")]
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

    [Theory]
    [InlineData("Lisboa", null, "PT", "OnSite", "Lisboa, Portugal")]
    [InlineData("Curitiba", "PR", "BR", "OnSite", "Curitiba/PR")]
    [InlineData(null, null, "ES", "Remote", "Espanha")]
    [InlineData(null, null, "BR", "OnSite", null)]
    public void JobPlace_NamesTheCountryOnlyWhenAbroad(string? city, string? state, string country, string type, string? expected) =>
        Formatters.JobPlace(new JobResponse { City = city, State = state, Country = country, Type = type, IsRemote = type == "Remote" })
            .Should().Be(expected);

    [Theory]
    [InlineData("Remote", "Global", null, "De qualquer país")]
    [InlineData("Hybrid", "Restricted", "PT,ES", "Só de: Portugal, Espanha")]
    [InlineData("Remote", null, null, "Não informado")]
    [InlineData("OnSite", "Global", null, null)]
    public void RemoteScope_NeverAssumesGlobal(string type, string? scope, string? countries, string? expected) =>
        Formatters.RemoteScope(new JobResponse { Type = type, RemoteScope = scope, EligibleCountries = countries }).Should().Be(expected);

    [Theory]
    [InlineData(true, "Oferece")]
    [InlineData(false, "Não oferece")]
    [InlineData(null, "Não informado")]
    public void YesNoUnknown_KeepsNotInformedApartFromNo(bool? value, string expected) =>
        Formatters.YesNoUnknown(value, "Oferece", "Não oferece").Should().Be(expected);
}
