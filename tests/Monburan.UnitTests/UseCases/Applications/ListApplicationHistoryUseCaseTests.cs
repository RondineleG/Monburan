using FluentAssertions;
using NSubstitute;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Applications;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Applications;

public class ListApplicationHistoryUseCaseTests
{
    private readonly IBaseRepository<Application> _applications = Substitute.For<IBaseRepository<Application>>();
    private readonly IApplicationStatusHistoryRepository _history = Substitute.For<IApplicationStatusHistoryRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _advertiserId = Guid.NewGuid();
    private readonly Guid _applicationId = Guid.NewGuid();
    private readonly ListApplicationHistoryUseCase _useCase;

    public ListApplicationHistoryUseCaseTests()
    {
        _useCase = new ListApplicationHistoryUseCase(_applications, _history);
    }

    private Application ApplicationOf() => new()
    {
        Id = Id.Create(_applicationId),
        CandidateId = Id.Create(_candidateId),
        JobId = Id.Create(Guid.NewGuid()),
        Status = EApplicationStatus.Viewed,
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

    private ApplicationStatusHistory HistoryOf(EApplicationStatus status, string? note = null, string? responsible = null) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        ApplicationId = Id.Create(_applicationId),
        Status = status,
        Date = DateTime.UtcNow.AddDays(-1),
        Note = note,
        Responsible = responsible,
        TenantId = "default"
    };

    [Fact]
    public async Task ExecuteAsync_ForTheCandidateWhoApplied_ReturnsTheHistoryInOrder()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));
        _history.ListByApplicationAsync(_applicationId).Returns(RequestResult<IEnumerable<ApplicationStatusHistory>>.Success(
        [
            HistoryOf(EApplicationStatus.Sent),
            HistoryOf(EApplicationStatus.Viewed, "Currículo visto", "ana@empresa.com")
        ]));

        var result = await _useCase.ExecuteAsync(new ListApplicationHistoryRequest(_applicationId, _candidateId, null));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().HaveCount(2);
        result.Data!.Select(item => item.Status).Should().ContainInOrder("Sent", "Viewed");
        result.Data.Last().Note.Should().Be("Currículo visto");
        result.Data.Last().Responsible.Should().Be("ana@empresa.com");
    }

    [Fact]
    public async Task ExecuteAsync_ForTheJobAdvertiser_ReturnsTheHistory()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));
        _history.ListByApplicationAsync(_applicationId).Returns(RequestResult<IEnumerable<ApplicationStatusHistory>>.Success([HistoryOf(EApplicationStatus.Sent)]));

        var result = await _useCase.ExecuteAsync(new ListApplicationHistoryRequest(_applicationId, null, _advertiserId));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_ForAStranger_BehavesAsNotFoundAndNeverReadsTheHistory()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));

        var result = await _useCase.ExecuteAsync(new ListApplicationHistoryRequest(_applicationId, Guid.NewGuid(), Guid.NewGuid()));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _history.DidNotReceiveWithAnyArgs().ListByApplicationAsync(default);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheApplicationDoesNotExist_ReturnsNotFound()
    {
        _applications.GetByIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<Application>.EntityNotFound("Application", _applicationId, "Candidatura não encontrada."));

        var result = await _useCase.ExecuteAsync(new ListApplicationHistoryRequest(_applicationId, _candidateId, null));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _history.DidNotReceiveWithAnyArgs().ListByApplicationAsync(default);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheHistoryLookupFails_PropagatesTheError()
    {
        _applications.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Application>.Success(ApplicationOf()));
        _history.ListByApplicationAsync(_applicationId)
            .Returns(RequestResult<IEnumerable<ApplicationStatusHistory>>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new ListApplicationHistoryRequest(_applicationId, _candidateId, null));

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
