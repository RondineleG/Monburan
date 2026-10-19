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

public class CreateApplicationUseCaseTests
{
    private readonly IBaseRepository<Application> _applications = Substitute.For<IBaseRepository<Application>>();
    private readonly IBaseRepository<Job> _jobs = Substitute.For<IBaseRepository<Job>>();
    private readonly IApplicationStatusHistoryRepository _history = Substitute.For<IApplicationStatusHistoryRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly CreateApplicationUseCase _useCase;

    public CreateApplicationUseCaseTests()
    {
        _jobs.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(ActiveJob()));
        _applications.CreateAsync(Arg.Any<Application>())
            .Returns(call => RequestResult<Application>.Success(call.Arg<Application>()));
        _history.CreateAsync(Arg.Any<ApplicationStatusHistory>())
            .Returns(call => RequestResult<ApplicationStatusHistory>.Success(call.Arg<ApplicationStatusHistory>()));

        var notifications = new NotifyOnApplicationCreatedUseCase(
            Substitute.For<IBaseRepository<Application>>(),
            Substitute.For<IBaseRepository<Job>>(),
            Substitute.For<IBaseRepository<Advertiser>>(),
            new SendEmailNotificationUseCase(Substitute.For<IEmailSender>(), Substitute.For<ILogger<SendEmailNotificationUseCase>>()),
            new CreateNotificationUseCase(Substitute.For<IBaseRepository<Notification>>()),
            Substitute.For<ILogger<NotifyOnApplicationCreatedUseCase>>());

        _useCase = new CreateApplicationUseCase(
            _applications,
            _jobs,
            Substitute.For<IBaseRepository<Resume>>(),
            _history,
            notifications,
            Substitute.For<ILogger<CreateApplicationUseCase>>());
    }

    private Job ActiveJob() => new()
    {
        Id = Id.Create(_jobId),
        AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = "Vaga",
        Description = "Descrição",
        Status = EJobStatus.Active,
        ExpirationDate = DateTime.UtcNow.AddDays(10),
        TenantId = "default"
    };

    private CreateApplicationRequest Request() => new()
    {
        CandidateId = _candidateId,
        JobId = _jobId,
        Notes = "Tenho interesse."
    };

    [Fact]
    public async Task ExecuteAsync_WhenTheApplicationIsCreated_WritesSentToTheHistory()
    {
        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
        await _history.Received(1).CreateAsync(Arg.Is<ApplicationStatusHistory>(history =>
            history.ApplicationId.Value == result.Data!.Id
            && history.Status == EApplicationStatus.Sent
            && history.TenantId == "default"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheHistoryWriteFails_StillCreatesTheApplication()
    {
        _history.CreateAsync(Arg.Any<ApplicationStatusHistory>())
            .Returns(RequestResult<ApplicationStatusHistory>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
        await _applications.Received(1).CreateAsync(Arg.Any<Application>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheHistoryWriteThrows_StillCreatesTheApplication()
    {
        _history.CreateAsync(Arg.Any<ApplicationStatusHistory>())
            .Returns(Task.FromException<RequestResult<ApplicationStatusHistory>>(new InvalidOperationException("tabela ausente")));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
    }
}
