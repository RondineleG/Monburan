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
    private readonly IBaseRepository<Application> _applications = Substitute.For<IBaseRepository<Application>>();
    private readonly IApplicationStatusHistoryRepository _history = Substitute.For<IApplicationStatusHistoryRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _advertiserId = Guid.NewGuid();
    private readonly Guid _applicationId = Guid.NewGuid();
    private readonly UpdateApplicationStatusUseCase _useCase;

    public UpdateApplicationStatusUseCaseTests()
    {
        _applications.UpdateAsync(Arg.Any<Application>())
            .Returns(call => RequestResult<Application>.Success(call.Arg<Application>()));
        _history.CreateAsync(Arg.Any<ApplicationStatusHistory>())
            .Returns(call => RequestResult<ApplicationStatusHistory>.Success(call.Arg<ApplicationStatusHistory>()));

        var notifications = new NotifyOnApplicationStatusChangedUseCase(
            Substitute.For<IBaseRepository<Application>>(),
            Substitute.For<IBaseRepository<Candidate>>(),
            Substitute.For<IBaseRepository<Job>>(),
            new SendEmailNotificationUseCase(Substitute.For<IEmailSender>(), Substitute.For<ILogger<SendEmailNotificationUseCase>>()),
            new CreateNotificationUseCase(Substitute.For<IBaseRepository<Notification>>()),
            Substitute.For<ILogger<NotifyOnApplicationStatusChangedUseCase>>());

        _useCase = new UpdateApplicationStatusUseCase(
            _applications,
            _history,
            notifications,
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
    public async Task ExecuteAsync_WhenTheStatusChanges_WritesTheHistoryWithNoteAndResponsible()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));

        var result = await _useCase.ExecuteAsync(Request(EApplicationStatus.Viewed));

        result.Status.Should().Be(EResultStatus.Success);
        await _history.Received(1).CreateAsync(Arg.Is<ApplicationStatusHistory>(history =>
            history.ApplicationId.Value == _applicationId
            && history.Status == EApplicationStatus.Viewed
            && history.Note == "Perfil aprovado."
            && history.Responsible == "ana@empresa.com"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheStatusDoesNotChange_DoesNotWriteTheHistory()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf(EApplicationStatus.Viewed)));

        var result = await _useCase.ExecuteAsync(Request(EApplicationStatus.Viewed));

        result.Status.Should().Be(EResultStatus.Success);
        await _history.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheHistoryWriteFails_StillUpdatesTheStatus()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));
        _history.CreateAsync(Arg.Any<ApplicationStatusHistory>())
            .Returns(RequestResult<ApplicationStatusHistory>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(Request(EApplicationStatus.Selected));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Status.Should().Be("Selected");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheApplicationBelongsToAnotherAdvertiser_ReturnsNotFoundAndNeverWrites()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));
        var request = Request(EApplicationStatus.Viewed);
        request.AdvertiserId = Guid.NewGuid();

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _applications.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await _history.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }
}
