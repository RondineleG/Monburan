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

public class ListSavedJobsUseCaseTests
{
    private readonly ISavedJobRepository _savedJobs = Substitute.For<ISavedJobRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly ListSavedJobsUseCase _useCase;

    public ListSavedJobsUseCaseTests()
    {
        _useCase = new ListSavedJobsUseCase(_savedJobs);
    }

    private SavedJob SavedJobOf(string title) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        CandidateId = Id.Create(_candidateId),
        JobId = Id.Create(Guid.NewGuid()),
        Job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = title,
            Description = "Descrição",
            Status = EJobStatus.Active,
            ExpirationDate = DateTime.UtcNow.AddDays(10),
            TenantId = "default"
        }
    };

    [Fact]
    public async Task ExecuteAsync_WhenThereAreSavedJobs_ReturnsThemMarkedAsSaved()
    {
        var first = SavedJobOf("Primeira");
        var second = SavedJobOf("Segunda");
        _savedJobs.ListByCandidateAsync(_candidateId)
            .Returns(RequestResult<IEnumerable<SavedJob>>.Success([first, second]));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Jobs.Should().HaveCount(2);
        result.Data.Jobs.Select(job => job.Title).Should().ContainInOrder("Primeira", "Segunda");
        result.Data.Jobs.Should().OnlyContain(job => job.IsSaved);
        result.Data.TotalCount.Should().Be(2);
        result.Data.Page.Should().Be(1);
        result.Data.Size.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheJobWasDeleted_SkipsTheOrphanEntry()
    {
        var saved = SavedJobOf("Primeira");
        saved.Job = null;
        _savedJobs.ListByCandidateAsync(_candidateId)
            .Returns(RequestResult<IEnumerable<SavedJob>>.Success([saved]));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Jobs.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheError()
    {
        _savedJobs.ListByCandidateAsync(_candidateId).Returns(RequestResult<IEnumerable<SavedJob>>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
