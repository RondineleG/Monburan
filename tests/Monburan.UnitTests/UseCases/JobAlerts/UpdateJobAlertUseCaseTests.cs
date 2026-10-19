using FluentAssertions;
using NSubstitute;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.JobAlerts;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.JobAlerts;

public class UpdateJobAlertUseCaseTests
{
    private readonly IJobAlertRepository _alerts = Substitute.For<IJobAlertRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _alertId = Guid.NewGuid();
    private readonly UpdateJobAlertUseCase _useCase;

    public UpdateJobAlertUseCaseTests()
    {
        _alerts.UpdateAsync(Arg.Any<JobAlert>())
            .Returns(call => RequestResult<JobAlert>.Success(call.Arg<JobAlert>()));

        _useCase = new UpdateJobAlertUseCase(_alerts);
    }

    private JobAlert AlertOf(Guid candidateId, bool isActive = true) => new()
    {
        Id = Id.Create(_alertId),
        CandidateId = Id.Create(candidateId),
        Name = "Antigo",
        State = "RJ",
        Mode = "OnSite",
        IsActive = isActive,
        TenantId = "default"
    };

    private static JobAlertRequest ValidRequest() => new()
    {
        Name = "  Novo nome  ",
        Keywords = "Frontend",
        City = "Curitiba",
        State = "pr",
        Mode = "hybrid",
        Category = "Tecnologia",
        SalaryMin = 4000
    };

    [Fact]
    public async Task ExecuteAsync_WhenTheAlertBelongsToTheCandidate_UpdatesTheFields()
    {
        var alert = AlertOf(_candidateId);
        _alerts.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<JobAlert>.Success(alert));

        var result = await _useCase.ExecuteAsync(new UpdateJobAlertCommand(_candidateId, _alertId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Name.Should().Be("Novo nome");
        result.Data.State.Should().Be("PR");
        result.Data.Mode.Should().Be("Hybrid");
        await _alerts.Received(1).UpdateAsync(Arg.Is<JobAlert>(updated =>
            updated.Id.Value == _alertId
            && updated.Name == "Novo nome"
            && updated.State == "PR"
            && updated.Mode == "Hybrid"
            && updated.IsActive));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutIsActiveInTheRequest_KeepsTheCurrentValue()
    {
        var alert = AlertOf(_candidateId, isActive: false);
        _alerts.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<JobAlert>.Success(alert));

        var result = await _useCase.ExecuteAsync(new UpdateJobAlertCommand(_candidateId, _alertId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheAlertDoesNotExist_ReturnsNotFound()
    {
        _alerts.GetByIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<JobAlert>.EntityNotFound("JobAlert", _alertId, "Alerta não encontrado."));

        var result = await _useCase.ExecuteAsync(new UpdateJobAlertCommand(_candidateId, _alertId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _alerts.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheAlertBelongsToSomeoneElse_BehavesAsNotFound()
    {
        _alerts.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<JobAlert>.Success(AlertOf(Guid.NewGuid())));

        var result = await _useCase.ExecuteAsync(new UpdateJobAlertCommand(_candidateId, _alertId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _alerts.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRequest_ReturnsValidationAndNeverReadsTheAlert()
    {
        var request = ValidRequest();
        request.State = "Paraná";

        var result = await _useCase.ExecuteAsync(new UpdateJobAlertCommand(_candidateId, _alertId, request));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == nameof(JobAlertRequest.State));
        await _alerts.DidNotReceiveWithAnyArgs().GetByIdAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheError()
    {
        _alerts.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<JobAlert>.Success(AlertOf(_candidateId)));
        _alerts.UpdateAsync(Arg.Any<JobAlert>()).Returns(RequestResult<JobAlert>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new UpdateJobAlertCommand(_candidateId, _alertId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
