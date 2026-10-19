using FluentAssertions;
using NSubstitute;
using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Jobs;

public class SearchJobsUseCaseTests
{
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly ISavedJobRepository _savedJobs = Substitute.For<ISavedJobRepository>();
    private readonly SearchJobsUseCase _useCase;

    public SearchJobsUseCaseTests()
    {
        _useCase = new SearchJobsUseCase(_jobs, _savedJobs);
    }

    private static Job JobOf(Guid jobId, string title) => new()
    {
        Id = Id.Create(jobId),
        AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = title,
        Description = "Descrição",
        Status = EJobStatus.Active,
        ExpirationDate = DateTime.UtcNow.AddDays(30),
        TenantId = "default"
    };

    private void ReturnsJobs(params Job[] jobs) =>
        _jobs.SearchAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(),
            Arg.Any<EContractType?>(), Arg.Any<ESalaryPeriod?>(), Arg.Any<int?>())
            .Returns(new PagedResponse<Job> { Data = jobs, TotalCount = jobs.Length, Page = 1, Size = 10 });


    [Fact]
    public async Task ExecuteAsync_WithAuthenticatedCandidate_MarksOnlyTheSavedJobs()
    {
        var candidateId = Guid.NewGuid();
        var savedJobId = Guid.NewGuid();
        ReturnsJobs(JobOf(savedJobId, "Salva"), JobOf(Guid.NewGuid(), "Não salva"));
        _savedJobs.ListSavedJobIdsAsync(candidateId, Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(RequestResult<IReadOnlyCollection<Guid>>.Success([savedJobId]));

        var result = await _useCase.ExecuteAsync(new ListJobsRequest { CandidateId = candidateId });

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Jobs.Should().ContainSingle(job => job.IsSaved).Which.Title.Should().Be("Salva");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCandidate_NeverQueriesSavedJobs()
    {
        ReturnsJobs(JobOf(Guid.NewGuid(), "Vaga"));

        var result = await _useCase.ExecuteAsync(new ListJobsRequest());

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Jobs.Should().OnlyContain(job => !job.IsSaved);
        await _savedJobs.DidNotReceiveWithAnyArgs().ListSavedJobIdsAsync(default, default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheSavedLookupFails_StillReturnsTheListing()
    {
        var candidateId = Guid.NewGuid();
        ReturnsJobs(JobOf(Guid.NewGuid(), "Vaga"));
        _savedJobs.ListSavedJobIdsAsync(candidateId, Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(RequestResult<IReadOnlyCollection<Guid>>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new ListJobsRequest { CandidateId = candidateId });

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Jobs.Should().OnlyContain(job => !job.IsSaved);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheSearchThrows_PropagatesTheException()
    {
        _jobs.SearchAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(),
            Arg.Any<EContractType?>(), Arg.Any<ESalaryPeriod?>(), Arg.Any<int?>())
            .Returns(Task.FromException<RequestResult<PagedResponse<Job>>>(new InvalidOperationException("banco fora do ar")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.ExecuteAsync(new ListJobsRequest()));
    }

    [Fact]
    public async Task ExecuteAsync_ParsesTheNewFiltersIgnoringTheCase()
    {
        ReturnsJobs();

        await _useCase.ExecuteAsync(new ListJobsRequest
        {
            ContractType = "pj",
            SalaryPeriod = "month",
            PublishedWithinDays = 7
        });

        await _jobs.Received(1).SearchAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(),
            EContractType.Pj, ESalaryPeriod.Month, 7);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownFilterValues_IgnoresThemInsteadOfFailing()
    {
        ReturnsJobs();

        await _useCase.ExecuteAsync(new ListJobsRequest
        {
            ContractType = "Banana",
            SalaryPeriod = "Século",
            PublishedWithinDays = 0
        });

        await _jobs.Received(1).SearchAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(),
            null, null, null);
    }
}
