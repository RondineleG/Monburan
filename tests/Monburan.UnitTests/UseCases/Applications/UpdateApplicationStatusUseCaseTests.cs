using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Applications;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Applications;

public class UpdateApplicationStatusUseCaseTests
{
    private readonly IApplicationRepository _applications = Substitute.For<IApplicationRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _advertiserId = Guid.NewGuid();
    private readonly Guid _applicationId = Guid.NewGuid();
    private readonly UpdateApplicationStatusUseCase _useCase;

    public UpdateApplicationStatusUseCaseTests()
    {
        _applications.UpdateStatusWithHistoryAsync(Arg.Any<Application>(), Arg.Any<ApplicationStatusHistory>(), Arg.Any<CancellationToken>())
            .Returns(call => RequestResult<Application>.Success(call.Arg<Application>()));

        var notifications = new NotifyOnApplicationStatusChangedUseCase(
            Substitute.For<IBaseRepository<Application>>(),
            Substitute.For<IBaseRepository<Candidate>>(),
            Substitute.For<IBaseRepository<Job>>(),
            new SendEmailNotificationUseCase(Substitute.For<IEmailSender>(), Substitute.For<ILogger<SendEmailNotificationUseCase>>()),
            new CreateNotificationUseCase(Substitute.For<IBaseRepository<Notification>>()),
            Substitute.For<ILogger<NotifyOnApplicationStatusChangedUseCase>>());

        _useCase = new UpdateApplicationStatusUseCase(
            _applications,
            notifications,
            Substitute.For<ICurrentPrincipalAccessor>(),
            Substitute.For<ILogger<UpdateApplicationStatusUseCase>>());
    }

    private Application ApplicationOf(EApplicationStatus status = EApplicationStatus.Sent) => new()
    {
        Id = Id.Create(_applicationId),
        CandidateId = Id.Create(_candidateId),
        JobId = Id.Create(Guid.NewGuid()),
        Status = status,
        AppliedAt = DateTime.UtcNow.AddDays(-2),
        TenantId = "default",
        Job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(_advertiserId),
            Title = "Vaga",
            Description = "Descrição",
            Status = EJobStatus.Active,
            ExpirationDate = DateTime.UtcNow.AddDays(10),
            TenantId = "default"
        }
    };

    private UpdateApplicationStatusRequest Request(EApplicationStatus newStatus) => new()
    {
        ApplicationId = _applicationId,
        AdvertiserId = _advertiserId,
        NewStatus = newStatus,
        Notes = "Perfil aprovado.",
        Responsible = "ana@empresa.com"
    };

    [Fact]
    public async Task ExecuteAsync_WhenTheStatusChanges_WritesTheHistoryAtomicallyWithNoteAndResponsible()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));

        var result = await _useCase.ExecuteAsync(Request(EApplicationStatus.Viewed));

        result.Status.Should().Be(EResultStatus.Success);
        await _applications.Received(1).UpdateStatusWithHistoryAsync(
            Arg.Is<Application>(application => application.Status == EApplicationStatus.Viewed && application.Version == 2),
            Arg.Is<ApplicationStatusHistory>(history =>
                history.ApplicationId.Value == _applicationId
                && history.Status == EApplicationStatus.Viewed
                && history.Version == 2
                && history.Note == "Perfil aprovado."
                && history.Responsible == "ana@empresa.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheStatusDoesNotChange_DoesNotWriteAnything()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf(EApplicationStatus.Viewed)));

        var result = await _useCase.ExecuteAsync(Request(EApplicationStatus.Viewed));

        result.Status.Should().Be(EResultStatus.Success);
        await _applications.DidNotReceiveWithAnyArgs().UpdateStatusWithHistoryAsync(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheAtomicUpdateFails_DoesNotReportSuccess()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));
        _applications.UpdateStatusWithHistoryAsync(Arg.Any<Application>(), Arg.Any<ApplicationStatusHistory>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Application>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(Request(EApplicationStatus.Selected));

        result.Status.Should().Be(EResultStatus.HasError);
    }


    [Fact]
    public async Task ExecuteAsync_WhenTheNoteOrResponsibleIsTooLong_ReturnsValidationWithoutWriting()
    {
        var request = Request(EApplicationStatus.Viewed);
        request.Notes = new string('x', ApplicationStatusHistory.MaxNoteLength + 1);
        request.Responsible = new string('r', ApplicationStatusHistory.MaxResponsibleLength + 1);

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == nameof(request.Notes));
        result.ValidationResult.Errors.Should().Contain(error => error.Field == nameof(request.Responsible));
        await _applications.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default);
    }


    [Fact]
    public async Task ExecuteAsync_WhenUnicodeTextFitsTheCharacterLimit_UpdatesTheStatus()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));
        var request = Request(EApplicationStatus.Viewed);
        request.Notes = new string('x', ApplicationStatusHistory.MaxNoteLength - 1) + "😀";

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheApplicationBelongsToAnotherAdvertiser_ReturnsNotFoundAndNeverWrites()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));
        var request = Request(EApplicationStatus.Viewed);
        request.AdvertiserId = Guid.NewGuid();

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _applications.DidNotReceiveWithAnyArgs().UpdateStatusWithHistoryAsync(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheTransitionGoesBackward_ReturnsConflictWithoutWriting()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf(EApplicationStatus.Rejected)));

        var result = await _useCase.ExecuteAsync(Request(EApplicationStatus.Viewed));

        result.Status.Should().Be(EResultStatus.Conflict);
        await _applications.DidNotReceiveWithAnyArgs().UpdateStatusWithHistoryAsync(default!, default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheTerminalStateIsArchived_AllowsTheTransition()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf(EApplicationStatus.Selected)));

        var result = await _useCase.ExecuteAsync(Request(EApplicationStatus.Archived));

        result.Status.Should().Be(EResultStatus.Success);
        await _applications.Received(1).UpdateStatusWithHistoryAsync(
            Arg.Is<Application>(a => a.Status == EApplicationStatus.Archived),
            Arg.Any<ApplicationStatusHistory>(),
            Arg.Any<CancellationToken>());
    }
}
