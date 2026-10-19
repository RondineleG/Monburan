using FluentAssertions;
using NSubstitute;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.JobAlerts;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.JobAlerts;

public class DeleteJobAlertUseCaseTests
{
    private readonly IJobAlertRepository _alerts = Substitute.For<IJobAlertRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _alertId = Guid.NewGuid();
    private readonly DeleteJobAlertUseCase _useCase;

    public DeleteJobAlertUseCaseTests()
    {
        _useCase = new DeleteJobAlertUseCase(_alerts);
    }

    private JobAlert AlertOf(Guid candidateId) => new()
    {
        Id = Id.Create(_alertId),
        CandidateId = Id.Create(candidateId),
        Name = "Alerta",
        TenantId = "default"
    };

    [Fact]
    public async Task ExecuteAsync_WhenTheAlertBelongsToTheCandidate_DeletesIt()
    {
        var alert = AlertOf(_candidateId);
        _alerts.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<JobAlert>.Success(alert));
        _alerts.DeleteAsync(alert.Id).Returns(RequestResult<JobAlert>.Success(alert));

        var result = await _useCase.ExecuteAsync(new DeleteJobAlertCommand(_candidateId, _alertId));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeTrue();
        await _alerts.Received(1).DeleteAsync(alert.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheAlertDoesNotExist_ReturnsNotFound()
    {
        _alerts.GetByIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<JobAlert>.EntityNotFound("JobAlert", _alertId, "Alerta não encontrado."));

        var result = await _useCase.ExecuteAsync(new DeleteJobAlertCommand(_candidateId, _alertId));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _alerts.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheAlertBelongsToSomeoneElse_BehavesAsNotFound()
    {
        _alerts.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<JobAlert>.Success(AlertOf(Guid.NewGuid())));

        var result = await _useCase.ExecuteAsync(new DeleteJobAlertCommand(_candidateId, _alertId));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _alerts.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheError()
    {
        var alert = AlertOf(_candidateId);
        _alerts.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<JobAlert>.Success(alert));
        _alerts.DeleteAsync(alert.Id).Returns(RequestResult<JobAlert>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new DeleteJobAlertCommand(_candidateId, _alertId));

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
