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

public class RegisterJobViewUseCaseTests
{
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly Guid _advertiserId = Guid.NewGuid();
    private readonly RegisterJobViewUseCase _useCase;

    public RegisterJobViewUseCaseTests()
    {
        _jobs.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(Job()));
        _jobs.IncrementViewsAsync(Arg.Any<Guid>()).Returns(RequestResult<bool>.Success(true));

        _useCase = new RegisterJobViewUseCase(_jobs);
    }

    private Job Job() => new()
    {
        Id = Id.Create(_jobId),
        AdvertiserId = Id.Create(_advertiserId),
        Title = "Desenvolvedor",
        Description = "Descrição",
        Status = EJobStatus.Active,
        ExpirationDate = DateTime.UtcNow.AddDays(10),
        TenantId = "tenant-1"
    };

    [Fact]
    public async Task ExecuteAsync_WhenViewerIsAnonymous_IncrementsTheCounter()
    {
        var result = await _useCase.ExecuteAsync(new RegisterJobViewRequest(_jobId, null));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeTrue();
        await _jobs.Received(1).IncrementViewsAsync(_jobId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenViewerIsTheOwner_DoesNotCountTheView()
    {
        var result = await _useCase.ExecuteAsync(new RegisterJobViewRequest(_jobId, _advertiserId));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeTrue();
        await _jobs.DidNotReceive().IncrementViewsAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenViewerIsSomeoneElse_IncrementsTheCounter()
    {
        var result = await _useCase.ExecuteAsync(new RegisterJobViewRequest(_jobId, Guid.NewGuid()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeTrue();
        await _jobs.Received(1).IncrementViewsAsync(_jobId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobDoesNotExist_ReturnsNotFoundAndNeverIncrements()
    {
        _jobs.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.EntityNotFound("Job", _jobId, "Vaga não encontrada."));

        var result = await _useCase.ExecuteAsync(new RegisterJobViewRequest(_jobId, null));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _jobs.DidNotReceive().IncrementViewsAsync(Arg.Any<Guid>());
    }
}
