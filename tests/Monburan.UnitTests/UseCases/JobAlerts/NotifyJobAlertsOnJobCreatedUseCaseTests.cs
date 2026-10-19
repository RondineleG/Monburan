using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.JobAlerts;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.JobAlerts;

public class NotifyJobAlertsOnJobCreatedUseCaseTests
{
    private readonly IJobAlertRepository _alerts = Substitute.For<IJobAlertRepository>();
    private readonly IBaseRepository<Notification> _notifications = Substitute.For<IBaseRepository<Notification>>();
    private readonly NotifyJobAlertsOnJobCreatedUseCase _useCase;

    public NotifyJobAlertsOnJobCreatedUseCaseTests()
    {
        _notifications.CreateAsync(Arg.Any<Notification>())
            .Returns(call => RequestResult<Notification>.Success(call.Arg<Notification>()));
        _alerts.UpdateAsync(Arg.Any<JobAlert>())
            .Returns(call => RequestResult<JobAlert>.Success(call.Arg<JobAlert>()));

        _useCase = new NotifyJobAlertsOnJobCreatedUseCase(
            _alerts,
            new CreateNotificationUseCase(_notifications),
            Substitute.For<ILogger<NotifyJobAlertsOnJobCreatedUseCase>>());
    }

    private static Job JobOf(EJobStatus status = EJobStatus.Active) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = "Desenvolvedor Backend",
        Description = "Vaga para time de plataforma.",
        Category = "Tecnologia",
        Status = status,
        ExpirationDate = DateTime.UtcNow.AddDays(10),
        TenantId = "default",
        Location = new JobLocation { City = "São Paulo", State = "SP", Type = "Remote", Country = "BR" }
    };

    private static JobAlert AlertOf(string name = "Backend remoto", string? keywords = "backend") => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        CandidateId = Id.Create(Guid.NewGuid()),
        Name = name,
        Keywords = keywords,
        IsActive = true,
        TenantId = "default"
    };

    private void ReturnsAlerts(params JobAlert[] alerts) =>
        _alerts.ListActiveAsync().Returns(RequestResult<IEnumerable<JobAlert>>.Success(alerts));

    [Fact]
    public async Task ExecuteAsync_WhenAnAlertMatches_CreatesTheNotificationAndUpdatesLastMatchedAt()
    {
        var alert = AlertOf();
        ReturnsAlerts(alert);

        var result = await _useCase.ExecuteAsync(new NotifyJobAlertsOnJobCreatedRequest(JobOf()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().Be(1);
        await _notifications.Received(1).CreateAsync(Arg.Is<Notification>(notification =>
            notification.Type == ENotificationType.NewJob
            && notification.Message == "Nova vaga para o seu alerta: Backend remoto"
            && notification.RecipientId == alert.CandidateId.Value.ToString()));
        alert.LastMatchedAt.Should().NotBeNull();
        await _alerts.Received(1).UpdateAsync(alert);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoAlertMatches_DoesNotNotifyAnything()
    {
        ReturnsAlerts(AlertOf(keywords: "fotografia"));

        var result = await _useCase.ExecuteAsync(new NotifyJobAlertsOnJobCreatedRequest(JobOf()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().Be(0);
        await _notifications.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await _alerts.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheJobIsNotActive_DoesNotEvenListTheAlerts()
    {
        var result = await _useCase.ExecuteAsync(new NotifyJobAlertsOnJobCreatedRequest(JobOf(EJobStatus.Paused)));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().Be(0);
        await _alerts.DidNotReceiveWithAnyArgs().ListActiveAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneNotificationFails_TheOthersStillGetEvaluated()
    {
        var failing = AlertOf(name: "Backend");
        var working = AlertOf(name: "Remoto", keywords: "plataforma");
        ReturnsAlerts(failing, working);
        _notifications.CreateAsync(Arg.Is<Notification>(notification => notification.RecipientId == failing.CandidateId.Value.ToString()))
            .Returns(RequestResult<Notification>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new NotifyJobAlertsOnJobCreatedRequest(JobOf()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().Be(1);
        await _notifications.Received(1).CreateAsync(Arg.Is<Notification>(notification =>
            notification.RecipientId == working.CandidateId.Value.ToString()));
        failing.LastMatchedAt.Should().BeNull();
        working.LastMatchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheAlertLookupFails_PropagatesTheError()
    {
        _alerts.ListActiveAsync().Returns(RequestResult<IEnumerable<JobAlert>>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new NotifyJobAlertsOnJobCreatedRequest(JobOf()));

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
