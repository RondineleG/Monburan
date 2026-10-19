using FluentAssertions;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.Services;

public class JobAlertMatcherTests
{
    private static Job JobOf(
        string title = "Desenvolvedor Backend",
        string? tags = "C#, .NET",
        string? category = "Tecnologia",
        string? description = "Vaga para time de plataforma.",
        string? city = "São Paulo",
        string? state = "SP",
        string? mode = "Remote",
        decimal? salaryMin = 5000,
        decimal? salaryMax = 9000) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = title,
        Tags = tags,
        Category = category,
        Description = description ?? string.Empty,
        Status = EJobStatus.Active,
        ExpirationDate = DateTime.UtcNow.AddDays(10),
        TenantId = "default",
        Location = new JobLocation { City = city, State = state, Type = mode ?? "OnSite", Country = "BR" },
        Salary = salaryMin is null && salaryMax is null
            ? null
            : new SalaryRange { Min = salaryMin, Max = salaryMax, Currency = "BRL", Display = true }
    };

    private static JobAlert AlertOf(
        string? keywords = null,
        string? city = null,
        string? state = null,
        string? mode = null,
        string? category = null,
        decimal? salaryMin = null,
        bool isActive = true) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        CandidateId = Id.Create(Guid.NewGuid()),
        Name = "Meu alerta",
        Keywords = keywords,
        City = city,
        State = state,
        Mode = mode,
        Category = category,
        SalaryMin = salaryMin,
        IsActive = isActive,
        TenantId = "default"
    };

    [Fact]
    public void Matches_WithEverythingFilledAndCompatible_IsTrue()
    {
        var job = JobOf();
        var alert = AlertOf(keywords: "backend, plataforma", city: "São Paulo", state: "SP", mode: "Remote", category: "Tecnologia", salaryMin: 8000);

        JobAlertMatcher.Matches(job, alert).Should().BeTrue();
    }

    [Fact]
    public void Matches_WithAnEmptyAlert_IsTrueForAnyJob()
    {
        JobAlertMatcher.Matches(JobOf(), AlertOf()).Should().BeTrue();
    }

    [Fact]
    public void Matches_WithAnInactiveAlert_IsAlwaysFalse()
    {
        JobAlertMatcher.Matches(JobOf(), AlertOf(isActive: false)).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenAnyKeywordAppearsInAnyField_IsTrue()
    {
        var job = JobOf(title: "Analista", tags: null, category: null, description: "Projetos com Kubernetes.");

        JobAlertMatcher.Matches(job, AlertOf(keywords: "kubernetes")).Should().BeTrue();
        JobAlertMatcher.Matches(job, AlertOf(keywords: "Vue, Kubernetes")).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenNoKeywordAppears_IsFalse()
    {
        JobAlertMatcher.Matches(JobOf(), AlertOf(keywords: "fotografia, design")).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenTheCityIsFilledButDifferent_IsFalse()
    {
        JobAlertMatcher.Matches(JobOf(city: "Campinas"), AlertOf(city: "São Paulo")).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenTheStateIsFilledAndEqual_IgnoresTheCase()
    {
        JobAlertMatcher.Matches(JobOf(state: "SP"), AlertOf(state: "sp")).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenTheModeIsFilledButDifferent_IsFalse()
    {
        JobAlertMatcher.Matches(JobOf(mode: "OnSite"), AlertOf(mode: "Remote")).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenTheCategoryIsFilledButDifferent_IsFalse()
    {
        JobAlertMatcher.Matches(JobOf(category: "Tecnologia"), AlertOf(category: "Vendas")).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenTheAlertSalaryFitsTheJobMaximum_IsTrue()
    {
        JobAlertMatcher.Matches(JobOf(salaryMin: 5000, salaryMax: 9000), AlertOf(salaryMin: 8000)).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenTheAlertSalaryIsAboveTheJobMaximum_IsFalse()
    {
        JobAlertMatcher.Matches(JobOf(salaryMin: 3000, salaryMax: 5000), AlertOf(salaryMin: 8000)).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenTheJobDoesNotInformSalary_IsFalseForASalaryAlert()
    {
        JobAlertMatcher.Matches(JobOf(salaryMin: null, salaryMax: null), AlertOf(salaryMin: 1000)).Should().BeFalse();
    }
}
