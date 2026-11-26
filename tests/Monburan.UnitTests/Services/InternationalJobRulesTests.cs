using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Services;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Services;

public class InternationalJobRulesTests
{
    private static Job JobWith(string type, ERemoteScope? scope = null, string? countries = null, string country = "BR", string currency = "BRL") => new()
    {
        Title = "Vaga",
        Description = "Descrição da vaga",
        Location = new JobLocation { Type = type, Country = country },
        Salary = new SalaryRange { Currency = currency },
        RemoteScope = scope,
        EligibleCountries = countries
    };

    [Fact]
    public void Normalize_UppercasesCodesAndDropsDuplicates()
    {
        var job = JobWith("Remote", ERemoteScope.Restricted, "pt, es; PT ie", country: " pt ", currency: "eur");

        InternationalJobRules.Normalize(job);

        job.Location!.Country.Should().Be("PT");
        job.Salary!.Currency.Should().Be("EUR");
        job.EligibleCountries.Should().Be("PT,ES,IE");
    }

    [Fact]
    public void Normalize_OnSiteJobHasNoRemoteScope()
    {
        var job = JobWith("OnSite", ERemoteScope.Global, "PT");

        InternationalJobRules.Normalize(job);

        job.RemoteScope.Should().BeNull();
        job.EligibleCountries.Should().BeNull();
    }

    [Fact]
    public void Normalize_CountriesOnlyWhenRestricted()
    {
        var job = JobWith("Remote", ERemoteScope.Global, "PT,ES");

        InternationalJobRules.Normalize(job);

        job.RemoteScope.Should().Be(ERemoteScope.Global);
        job.EligibleCountries.Should().BeNull();
    }

    [Fact]
    public void Normalize_EmptyCountryAndCurrencyFallBackToDefaults()
    {
        var job = JobWith("OnSite", country: " ", currency: "");

        InternationalJobRules.Normalize(job);

        job.Location!.Country.Should().Be("BR");
        job.Salary!.Currency.Should().Be("BRL");
    }

    [Fact]
    public void Validate_RestrictedWithoutCountries_AsksForThem()
    {
        var job = JobWith("Remote", ERemoteScope.Restricted);
        InternationalJobRules.Normalize(job);

        InternationalJobRules.Validate(job).Should().ContainSingle(error => error.Field == "EligibleCountries");
    }

    [Fact]
    public void Validate_RejectsCountryNamesInsteadOfCodes()
    {
        var job = JobWith("Hybrid", ERemoteScope.Restricted, "Portugal");
        InternationalJobRules.Normalize(job);

        InternationalJobRules.Validate(job).Should().ContainSingle(error => error.Field == "EligibleCountries"
            && error.Message.Contains("PORTUGAL"));
    }

    [Theory]
    [InlineData("Brasil", "BRL", "Country")]
    [InlineData("BR", "R$", "SalaryCurrency")]
    public void Validate_RequiresIsoCodes(string country, string currency, string field)
    {
        var job = JobWith("OnSite", country: country, currency: currency);
        InternationalJobRules.Normalize(job);

        InternationalJobRules.Validate(job).Should().ContainSingle(error => error.Field == field);
    }

    [Fact]
    public void Validate_NotInformedEverywhereIsValid()
    {
        var job = JobWith("Remote");
        InternationalJobRules.Normalize(job);

        InternationalJobRules.Validate(job).Should().BeEmpty();
    }

    [Fact]
    public void WizardStepOne_RejectsRestrictedRemoteWithoutCountries()
    {
        var model = new CreateJobRequest { Title = "Vaga", Type = "Remote", RemoteScope = ERemoteScope.Restricted };

        JobWizardStepValidator.Validate(1, model).Should().ContainSingle(error => error.Field == nameof(CreateJobRequest.EligibleCountries));
        JobWizardStepValidator.StepOf(nameof(CreateJobRequest.EligibleCountries)).Should().Be(1);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void WizardTriState_EmptyIsNotInformed_NeverNo(string? value, bool? expected) =>
        Monburan.Web.Components.Shared.JobWizard.ParseTriState(value).Should().Be(expected);

    [Fact]
    public void FlagOff_CreateDropsTheNewFields()
    {
        var request = new CreateJobRequest { RemoteScope = ERemoteScope.Global, EligibleCountries = "PT", OffersRelocation = true, SponsorsVisa = false };

        InternationalJobFields.DropUnlessEnabled(request, enabled: false);

        request.RemoteScope.Should().BeNull();
        request.EligibleCountries.Should().BeNull();
        request.OffersRelocation.Should().BeNull();
        request.SponsorsVisa.Should().BeNull();
    }

    [Fact]
    public void FlagOff_UpdateKeepsWhatWasSaved()
    {
        var request = new UpdateJobRequest { RemoteScope = null, OffersRelocation = null, SponsorsVisa = null };
        var existing = new Monburan.Core.DTOs.Response.JobResponse
        {
            RemoteScope = "Restricted", EligibleCountries = "PT,ES", OffersRelocation = true, SponsorsVisa = false
        };

        InternationalJobFields.KeepExistingUnlessEnabled(request, existing, enabled: false);

        request.RemoteScope.Should().Be(ERemoteScope.Restricted);
        request.EligibleCountries.Should().Be("PT,ES");
        request.OffersRelocation.Should().BeTrue();
        request.SponsorsVisa.Should().BeFalse();
    }
}
