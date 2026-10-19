using FluentAssertions;
using NSubstitute;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Jobs;

public class SaveJobUseCaseTests
{
    private readonly ISavedJobRepository _savedJobs = Substitute.For<ISavedJobRepository>();
    private readonly IBaseRepository<Job> _jobs = Substitute.For<IBaseRepository<Job>>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly SaveJobUseCase _useCase;

    public SaveJobUseCaseTests()
    {
        _jobs.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(ActiveJob()));
        _savedJobs.SaveAsync(Arg.Any<SavedJob>())
            .Returns(call => RequestResult<SavedJob>.Success(call.Arg<SavedJob>()));

        _useCase = new SaveJobUseCase(_savedJobs, _jobs);
    }

    private Job ActiveJob() => new()
    {
        Id = Id.Create(_jobId),
        AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = "Desenvolvedor",
        Description = "Descrição",
        Status = EJobStatus.Active,
        ExpirationDate = DateTime.UtcNow.AddDays(10),
        TenantId = "tenant-1"
    };

    [Fact]
    public async Task ExecuteAsync_WhenJobIsActive_SavesWithTheJobTenant()
    {
        var result = await _useCase.ExecuteAsync(new SaveJobRequest(_candidateId, _jobId));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeTrue();
        await _savedJobs.Received(1).SaveAsync(Arg.Is<SavedJob>(saved =>
            saved.CandidateId.Value == _candidateId
            && saved.JobId.Value == _jobId
            && saved.TenantId == "tenant-1"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobDoesNotExist_ReturnsNotFoundAndNeverSaves()
    {
        _jobs.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.EntityNotFound("Job", _jobId, "Vaga não encontrada."));

        var result = await _useCase.ExecuteAsync(new SaveJobRequest(_candidateId, _jobId));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _savedJobs.DidNotReceiveWithAnyArgs().SaveAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobIsInactive_ReturnsNotFoundAndNeverSaves()
    {
        var job = ActiveJob();
        job.Status = EJobStatus.Paused;
        _jobs.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(job));

        var result = await _useCase.ExecuteAsync(new SaveJobRequest(_candidateId, _jobId));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _savedJobs.DidNotReceiveWithAnyArgs().SaveAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobIsExpired_ReturnsNotFoundAndNeverSaves()
    {
        var job = ActiveJob();
        job.ExpirationDate = DateTime.UtcNow.AddDays(-1);
        _jobs.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(job));

        var result = await _useCase.ExecuteAsync(new SaveJobRequest(_candidateId, _jobId));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _savedJobs.DidNotReceiveWithAnyArgs().SaveAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheError()
    {
        _savedJobs.SaveAsync(Arg.Any<SavedJob>()).Returns(RequestResult<SavedJob>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new SaveJobRequest(_candidateId, _jobId));

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
