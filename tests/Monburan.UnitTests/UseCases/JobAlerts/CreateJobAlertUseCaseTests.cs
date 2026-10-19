using FluentAssertions;
using NSubstitute;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.JobAlerts;
using Xunit;

namespace Monburan.UnitTests.UseCases.JobAlerts;

public class CreateJobAlertUseCaseTests
{
    private readonly IJobAlertRepository _alerts = Substitute.For<IJobAlertRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly CreateJobAlertUseCase _useCase;

    public CreateJobAlertUseCaseTests()
    {
        _alerts.CountByCandidateAsync(_candidateId).Returns(RequestResult<int>.Success(0));
        _alerts.CreateAsync(Arg.Any<JobAlert>())
            .Returns(call => RequestResult<JobAlert>.Success(call.Arg<JobAlert>()));

        _useCase = new CreateJobAlertUseCase(_alerts);
    }

    private static JobAlertRequest ValidRequest() => new()
    {
        Name = "  Vagas remotas  ",
        Keywords = "Backend",
        City = "São Paulo",
        State = "sp",
        Mode = "remote",
        Category = "Tecnologia",
        SalaryMin = 5000
    };

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_NormalizesAndPersistsTheAlert()
    {
        var result = await _useCase.ExecuteAsync(new CreateJobAlertCommand(_candidateId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Vagas remotas");
        result.Data.State.Should().Be("SP");
        result.Data.Mode.Should().Be("Remote");
        result.Data.IsActive.Should().BeTrue();
        await _alerts.Received(1).CreateAsync(Arg.Is<JobAlert>(alert =>
            alert.CandidateId.Value == _candidateId
            && alert.Name == "Vagas remotas"
            && alert.State == "SP"
            && alert.Mode == "Remote"
            && alert.TenantId == "default"));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutName_ReturnsValidationAndNeverTouchesTheRepository()
    {
        var request = ValidRequest();
        request.Name = "   ";

        var result = await _useCase.ExecuteAsync(new CreateJobAlertCommand(_candidateId, request));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == nameof(JobAlertRequest.Name));
        await _alerts.DidNotReceiveWithAnyArgs().CountByCandidateAsync(default);
        await _alerts.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidMode_ReturnsValidation()
    {
        var request = ValidRequest();
        request.Mode = "Híbrido";

        var result = await _useCase.ExecuteAsync(new CreateJobAlertCommand(_candidateId, request));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == nameof(JobAlertRequest.Mode));
    }

    [Fact]
    public async Task ExecuteAsync_AtTheLimitOfTenAlerts_ReturnsConflict()
    {
        _alerts.CountByCandidateAsync(_candidateId).Returns(RequestResult<int>.Success(CreateJobAlertUseCase.MaxAlertsPerCandidate));

        var result = await _useCase.ExecuteAsync(new CreateJobAlertCommand(_candidateId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.EntityAlreadyExists);
        await _alerts.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCountFails_PropagatesTheErrorAndNeverCreates()
    {
        _alerts.CountByCandidateAsync(_candidateId).Returns(RequestResult<int>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new CreateJobAlertCommand(_candidateId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.HasError);
        await _alerts.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRepositoryFailsToCreate_PropagatesTheError()
    {
        _alerts.CreateAsync(Arg.Any<JobAlert>()).Returns(RequestResult<JobAlert>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new CreateJobAlertCommand(_candidateId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
