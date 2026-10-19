using FluentAssertions;
using NSubstitute;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Jobs;
using Xunit;

namespace Monburan.UnitTests.UseCases.Jobs;

public class UnsaveJobUseCaseTests
{
    private readonly ISavedJobRepository _savedJobs = Substitute.For<ISavedJobRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly UnsaveJobUseCase _useCase;

    public UnsaveJobUseCaseTests()
    {
        _useCase = new UnsaveJobUseCase(_savedJobs);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobWasSaved_RemovesAndReturnsTrue()
    {
        _savedJobs.RemoveAsync(_candidateId, _jobId).Returns(RequestResult<bool>.Success(true));

        var result = await _useCase.ExecuteAsync(new UnsaveJobRequest(_candidateId, _jobId));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeTrue();
        await _savedJobs.Received(1).RemoveAsync(_candidateId, _jobId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobWasNotSaved_StaysIdempotentAndReturnsTrue()
    {
        _savedJobs.RemoveAsync(_candidateId, _jobId).Returns(RequestResult<bool>.Success(false));

        var result = await _useCase.ExecuteAsync(new UnsaveJobRequest(_candidateId, _jobId));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheError()
    {
        _savedJobs.RemoveAsync(_candidateId, _jobId).Returns(RequestResult<bool>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new UnsaveJobRequest(_candidateId, _jobId));

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
