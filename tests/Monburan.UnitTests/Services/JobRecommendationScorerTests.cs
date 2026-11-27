using FluentAssertions;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.Services;

public class JobRecommendationScorerTests
{
    private static Job JobOf(
        string title = "Desenvolvedor",
        string? tags = null,
        string? category = null,
        string? description = null,
        string? city = null,
        string? state = null,
        string? mode = null,
        decimal? salaryMin = null,
        decimal? salaryMax = null) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = title,
        Tags = tags,
        Category = category,
        Description = description ?? "Descrição da vaga.",
        Status = EJobStatus.Active,
        ExpirationDate = DateTime.UtcNow.AddDays(10),
        TenantId = "default",
        Location = mode is null && city is null && state is null
            ? null
            : new JobLocation { City = city, State = state, Type = mode ?? "OnSite", Country = "BR" },
        Salary = salaryMin is null && salaryMax is null
            ? null
            : new SalaryRange { Min = salaryMin, Max = salaryMax, Currency = "BRL", Display = true }
    };

    private static CareerProfile ProfileOf(
        string? desiredRole = null,
        string? category = null,
        string? city = null,
        string? state = null,
        string? mode = null,
        decimal? minimumSalary = null) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        CandidateId = Id.Create(Guid.NewGuid()),
        DesiredRole = desiredRole,
        Category = category,
        City = city,
        State = state,
        Mode = mode,
        MinimumSalary = minimumSalary,
        TenantId = "default"
    };

    [Fact]
    public void Score_WithDesiredRoleInTheTitle_AddsThree()
    {
        var job = JobOf(title: "Pessoa Desenvolvedora Backend");
        var profile = ProfileOf(desiredRole: "Desenvolvedor Backend");

        JobRecommendationScorer.Score(job, profile, []).Should().Be(3);
    }

    [Fact]
    public void Score_WithDesiredRoleOnlyInTheTagsOrCategory_AlsoAddsThree()
    {
        var job = JobOf(title: "Vaga", tags: "dotnet, backend", category: "Backend");
        var profile = ProfileOf(desiredRole: "Backend");

        JobRecommendationScorer.Score(job, profile, []).Should().Be(3);
    }

    [Fact]
    public void Score_IgnoresRoleWordsShorterThanThreeLetters()
    {
        var job = JobOf(title: "Analista de Qualidade");
        var profile = ProfileOf(desiredRole: "QA");

        JobRecommendationScorer.Score(job, profile, []).Should().Be(0);
    }

    [Fact]
    public void Score_WithTheSameCategory_IsCaseInsensitiveAndAddsTwo()
    {
        var job = JobOf(category: "Tecnologia");
        var profile = ProfileOf(category: "tecnologia");

        JobRecommendationScorer.Score(job, profile, []).Should().Be(2);
    }

    [Fact]
    public void Score_WhenTheCityMatches_AddsTwoEvenIfTheStateDiffers()
    {
        var job = JobOf(city: "São Paulo", state: "RJ");
        var profile = ProfileOf(city: "são paulo", state: "SP");

        JobRecommendationScorer.Score(job, profile, []).Should().Be(2);
    }

    [Fact]
    public void Score_WhenOnlyTheStateMatches_AddsTwo()
    {
        var job = JobOf(city: "Campinas", state: "SP");
        var profile = ProfileOf(city: "São Paulo", state: "sp");

        JobRecommendationScorer.Score(job, profile, []).Should().Be(2);
    }

    [Fact]
    public void Score_WhenTheModeMatches_AddsOne()
    {
        var job = JobOf(mode: "Remote");
        var profile = ProfileOf(mode: "remote");

        JobRecommendationScorer.Score(job, profile, []).Should().Be(1);
    }


    [Fact]
    public void Evaluate_WhenTheSalaryIsHidden_DoesNotScoreOrExplainIt()
    {
        var job = JobOf(salaryMin: 5000, salaryMax: 9000);
        job.Salary!.Display = false;
        var profile = ProfileOf(minimumSalary: 8000);

        var evaluation = JobRecommendationScorer.Evaluate(job, profile, []);

        evaluation.Score.Should().Be(0);
        evaluation.Factors.Should().NotContain(factor => factor.Contains("Salário"));
    }

    [Fact]
    public void Score_WhenTheJobMaximumReachesTheMinimumSalary_AddsTwo()
    {
        var job = JobOf(salaryMin: 5000, salaryMax: 9000);
        var profile = ProfileOf(minimumSalary: 8000);

        JobRecommendationScorer.Score(job, profile, []).Should().Be(2);
    }

    [Fact]
    public void Score_WithoutAMaximum_UsesTheMinimumSalaryValue()
    {
        var job = JobOf(salaryMin: 9000);
        var profile = ProfileOf(minimumSalary: 8000);

        JobRecommendationScorer.Score(job, profile, []).Should().Be(2);
    }

    [Fact]
    public void Score_WhenTheJobDoesNotInformSalary_DoesNotScoreSalary()
    {
        var job = JobOf();
        var profile = ProfileOf(minimumSalary: 1000);

        JobRecommendationScorer.Score(job, profile, []).Should().Be(0);
    }

    [Fact]
    public void Score_WhenTheSalaryIsBelowTheMinimum_DoesNotScoreSalary()
    {
        var job = JobOf(salaryMin: 3000, salaryMax: 5000);
        var profile = ProfileOf(minimumSalary: 8000);

        JobRecommendationScorer.Score(job, profile, []).Should().Be(0);
    }

    [Fact]
    public void Score_AddsOnePerSkillFoundInTitleTagsOrDescription()
    {
        var job = JobOf(title: "Backend C#", tags: "SQL", description: "Projetos em Azure.");
        var skills = new[] { "C#", "SQL", "Azure" };

        JobRecommendationScorer.Score(job, profile: null, skills).Should().Be(3);
    }

    [Fact]
    public void Score_CapsTheSkillBonusAtThree()
    {
        var job = JobOf(title: "C# SQL Azure Docker Kubernetes");
        var skills = new[] { "C#", "SQL", "Azure", "Docker", "Kubernetes" };

        JobRecommendationScorer.Score(job, profile: null, skills).Should().Be(JobRecommendationScorer.MaxSkillBonus);
    }

    [Fact]
    public void Score_WithoutProfileAndWithoutSkills_IsZero()
    {
        JobRecommendationScorer.Score(JobOf(), profile: null, []).Should().Be(0);
    }

    [Fact]
    public void Score_WithoutProfile_StillCountsTheSkills()
    {
        var job = JobOf(title: "Backend .NET");

        JobRecommendationScorer.Score(job, profile: null, ["C#", ".NET"]).Should().Be(1);
    }
    [Fact]
    public void Evaluate_ReturnsTheFactorsThatComposeTheScore()
    {
        var job = JobOf(
            title: "Desenvolvedor Backend C#",
            category: "Tecnologia",
            city: "São Paulo",
            mode: "Remote",
            salaryMin: 7000,
            salaryMax: 9000);
        var profile = ProfileOf(
            desiredRole: "Desenvolvedor Backend",
            category: "Tecnologia",
            city: "São Paulo",
            mode: "Remote",
            minimumSalary: 8000);

        var evaluation = JobRecommendationScorer.Evaluate(job, profile, ["C#"]);

        evaluation.Score.Should().Be(11);
        evaluation.Factors.Should().Equal(
            "Cargo desejado encontrado na vaga (+3)",
            "Categoria compatível com o perfil (+2)",
            "Localização compatível com o perfil (+2)",
            "Modalidade compatível com o perfil (+1)",
            "Salário informado atinge o mínimo desejado (+2)",
            "Habilidade \"C#\" encontrada na vaga (+1)");
    }

}
