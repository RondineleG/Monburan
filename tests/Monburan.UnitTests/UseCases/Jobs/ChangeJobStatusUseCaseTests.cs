using FluentAssertions;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.ValueObjects;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Jobs;

public class ChangeJobStatusUseCaseTests
{
    private readonly Guid _advertiserId = Guid.NewGuid();
    private readonly IBaseRepository<Job> _jobs = Substitute.For<IBaseRepository<Job>>();
    private readonly IJobRepository _jobQueries = Substitute.For<IJobRepository>();
    private readonly ChangeJobStatusUseCase _useCase;

    public ChangeJobStatusUseCaseTests()
    {
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        subscriptions.GetActiveByAdvertiserIdAsync(Arg.Any<Id>()).Returns(RequestResult<Subscription?>.Success(null));
        var plans = Substitute.For<IBaseRepository<SubscriptionPlan>>();
        plans.GetAllAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(RequestResult<IEnumerable<SubscriptionPlan>>.Success(
            [new SubscriptionPlan { Name = CheckJobLimitUseCase.FreePlanName, MaxJobs = 5, IsActive = true }]));
        _jobQueries.CountActiveByAdvertiserAsync(Arg.Any<Guid>()).Returns(1);

        _jobs.UpdateAsync(Arg.Any<Job>()).Returns(call => RequestResult<Job>.Success(call.Arg<Job>()));
        _useCase = new ChangeJobStatusUseCase(_jobs, new CheckJobLimitUseCase(subscriptions, plans, _jobQueries));
    }

    [Fact]
    public async Task Pause_ActiveJob_Succeeds()
    {
        var job = Arrange(EJobStatus.Active, DateTime.UtcNow.AddDays(10));

        var result = await _useCase.ExecuteAsync(new ChangeJobStatusRequest(job.Id.Value, _advertiserId, EJobStatus.Paused));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Status.Should().Be("Paused");
    }

    [Fact]
    public async Task Reactivate_ExpiredJobWithPastDate_IsRejected()
    {
        var job = Arrange(EJobStatus.Expired, DateTime.UtcNow.AddDays(-2));

        var result = await _useCase.ExecuteAsync(new ChangeJobStatusRequest(job.Id.Value, _advertiserId, EJobStatus.Active));

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("data de expiração");
        await _jobs.DidNotReceive().UpdateAsync(Arg.Any<Job>());
    }

    [Fact]
    public async Task Reactivate_WhenPlanLimitReached_IsRejected()
    {
        var job = Arrange(EJobStatus.Paused, DateTime.UtcNow.AddDays(10));
        _jobQueries.CountActiveByAdvertiserAsync(_advertiserId).Returns(5);

        var result = await _useCase.ExecuteAsync(new ChangeJobStatusRequest(job.Id.Value, _advertiserId, EJobStatus.Active));

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("5 vagas ativas");
    }

    [Fact]
    public async Task JobOfAnotherAdvertiser_IsReportedAsNotFound()
    {
        var job = Arrange(EJobStatus.Active, DateTime.UtcNow.AddDays(10));

        var result = await _useCase.ExecuteAsync(new ChangeJobStatusRequest(job.Id.Value, Guid.NewGuid(), EJobStatus.Paused));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _jobs.DidNotReceive().UpdateAsync(Arg.Any<Job>());
    }

    [Fact]
    public async Task UnsupportedTargetStatus_IsValidationError()
    {
        var job = Arrange(EJobStatus.Active, DateTime.UtcNow.AddDays(10));

        var result = await _useCase.ExecuteAsync(new ChangeJobStatusRequest(job.Id.Value, _advertiserId, EJobStatus.Rejected));

        result.ValidationResult.Errors.Should().NotBeEmpty();
    }

    private Job Arrange(EJobStatus status, DateTime expiration)
    {
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(_advertiserId),
            Title = "Vaga",
            Description = "Descrição da vaga",
            Status = status,
            ExpirationDate = expiration,
            TenantId = "default"
        };
        _jobs.GetByIdAsync(Arg.Is<Id>(id => id == job.Id)).Returns(RequestResult<Job>.Success(job));
        return job;
    }
}
