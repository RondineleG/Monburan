using FluentAssertions;
using NSubstitute;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Jobs;

public class ListRecommendedJobsUseCaseTests
{
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly ICareerProfileRepository _profiles = Substitute.For<ICareerProfileRepository>();
    private readonly IResumeRepository _resumes = Substitute.For<IResumeRepository>();
    private readonly IApplicationRepository _applications = Substitute.For<IApplicationRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly ListRecommendedJobsUseCase _useCase;

    public ListRecommendedJobsUseCaseTests()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(null));
        _resumes.GetActiveWithSkillsAsync(_candidateId).Returns(RequestResult<Resume?>.Success(null));
        _applications.ListByCandidateAsync(_candidateId)
            .Returns(RequestResult<IEnumerable<Application>>.Success([]));

        _useCase = new ListRecommendedJobsUseCase(_jobs, _profiles, _resumes, _applications);
    }

    private static Job JobOf(
        string title,
        string? category = null,
        string? city = null,
        string? state = null,
        string? mode = null,
        string? tags = null,
        string? description = null,
        decimal? salaryMin = null,
        decimal? salaryMax = null)
    {
        return new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = title,
            Category = category,
            Tags = tags,
            Description = description ?? "Descrição",
            Status = EJobStatus.Active,
            ExpirationDate = DateTime.UtcNow.AddDays(10),
            TenantId = "default",
            Location = new JobLocation { City = city, State = state, Type = mode ?? "OnSite", Country = "BR" },
            Salary = salaryMin is null && salaryMax is null
                ? null
                : new SalaryRange { Min = salaryMin, Max = salaryMax, Currency = "BRL", Display = true }
        };
    }

    private CareerProfile ProfileOf(
        string? desiredRole = null,
        string? category = null,
        string? city = null,
        string? state = null,
        string? mode = null,
        decimal? minimumSalary = null) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        CandidateId = Id.Create(_candidateId),
        DesiredRole = desiredRole,
        Category = category,
        City = city,
        State = state,
        Mode = mode,
        MinimumSalary = minimumSalary,
        TenantId = "default"
    };

    private void ReturnsJobs(params Job[] jobs) =>
        _jobs.ListActiveForRecommendationsAsync().Returns(RequestResult<IEnumerable<Job>>.Success(jobs));

    [Fact]
    public async Task ExecuteAsync_WithoutProfileAndWithoutSkills_ReturnsEmptyWithoutQueryingJobs()
    {
        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Jobs.Should().BeEmpty();
        await _jobs.DidNotReceiveWithAnyArgs().ListActiveForRecommendationsAsync();
    }

    [Fact]
    public async Task ExecuteAsync_RanksByScoreAndDropsJobsWithZeroScore()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(
            ProfileOf(desiredRole: "Desenvolvedor Backend", category: "Tecnologia", city: "São Paulo", mode: "Remote", minimumSalary: 8000)));
        ReturnsJobs(
            JobOf("Desenvolvedor Backend", category: "Tecnologia", city: "São Paulo", mode: "Remote", tags: "C#", salaryMin: 5000, salaryMax: 9000),
            JobOf("Analista de Dados", category: "Tecnologia"),
            JobOf("Vendedor", category: "Vendas"));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Jobs.Select(job => job.Title).Should().ContainInOrder("Desenvolvedor Backend", "Analista de Dados");
        result.Data.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesJobsTheCandidateAlreadyAppliedTo()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(ProfileOf(category: "Tecnologia")));
        var alreadyApplied = JobOf("Vaga aplicada", category: "Tecnologia");
        var other = JobOf("Vaga aberta", category: "Tecnologia");
        ReturnsJobs(alreadyApplied, other);
        _applications.ListByCandidateAsync(_candidateId).Returns(RequestResult<IEnumerable<Application>>.Success(
        [
            new Application
            {
                Id = Id.Create(Guid.NewGuid()),
                CandidateId = Id.Create(_candidateId),
                JobId = alreadyApplied.Id,
                Status = EApplicationStatus.Sent,
                TenantId = "default"
            }
        ]));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Data!.Jobs.Should().ContainSingle(job => job.Title == "Vaga aberta");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutProfile_UsesTheActiveResumeSkills()
    {
        _resumes.GetActiveWithSkillsAsync(_candidateId).Returns(RequestResult<Resume?>.Success(new Resume
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(_candidateId),
            Title = "Currículo",
            IsActive = true,
            TenantId = "default",
            Skills = [new ResumeSkill { Skill = "C#", Level = "4" }]
        }));
        ReturnsJobs(JobOf("Backend C#", description: "Projetos em C# e .NET"), JobOf("Vendas"));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Jobs.Should().ContainSingle(job => job.Title == "Backend C#");
    }

    [Fact]
    public async Task ExecuteAsync_LimitsTheResultToTwentyJobs()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(ProfileOf(category: "Tecnologia")));
        ReturnsJobs(Enumerable.Range(0, 25)
            .Select(index => JobOf($"Vaga {index}", category: "Tecnologia"))
            .ToArray());

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Data!.Jobs.Should().HaveCount(ListRecommendedJobsUseCase.MaxResults);
        result.Data.TotalCount.Should().Be(ListRecommendedJobsUseCase.MaxResults);
        result.Data.Size.Should().Be(ListRecommendedJobsUseCase.MaxResults);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheProfileLookupFails_PropagatesTheError()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.HasError);
        await _jobs.DidNotReceiveWithAnyArgs().ListActiveForRecommendationsAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheApplicationsLookupFails_PropagatesTheError()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(ProfileOf(category: "Tecnologia")));
        ReturnsJobs(JobOf("Vaga", category: "Tecnologia"));
        _applications.ListByCandidateAsync(_candidateId).Returns(RequestResult<IEnumerable<Application>>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
