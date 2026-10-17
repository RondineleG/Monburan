using FluentAssertions;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.ValueObjects;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Subscriptions;

public class CheckJobLimitUseCaseTests
{
    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly IBaseRepository<SubscriptionPlan> _plans = Substitute.For<IBaseRepository<SubscriptionPlan>>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly CheckJobLimitUseCase _useCase;

    public CheckJobLimitUseCaseTests()
    {
        _subscriptions.GetActiveByAdvertiserIdAsync(Arg.Any<Id>()).Returns(RequestResult<Subscription?>.Success(null));
        _useCase = new CheckJobLimitUseCase(_subscriptions, _plans, _jobs);
    }

    [Fact]
    public async Task WithoutSubscription_UsesFreePlanLimit()
    {
        _plans.GetAllAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(RequestResult<IEnumerable<SubscriptionPlan>>.Success(
            [new SubscriptionPlan { Name = CheckJobLimitUseCase.FreePlanName, MaxJobs = 5, IsActive = true }]));
        _jobs.CountActiveByAdvertiserAsync(Arg.Any<Guid>()).Returns(5);

        var result = await _useCase.ExecuteAsync(new CheckJobLimitRequest(Guid.NewGuid()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.CanCreate.Should().BeFalse();
        result.Data.MaxJobs.Should().Be(5);
        result.Data.PlanName.Should().Be("Free");
    }

    [Fact]
    public async Task ActiveSubscription_UsesItsPlan()
    {
        var premium = new SubscriptionPlan { Name = "Premium", MaxJobs = 10, IsActive = true };
        _subscriptions.GetActiveByAdvertiserIdAsync(Arg.Any<Id>()).Returns(
            RequestResult<Subscription?>.Success(new Subscription { PlanId = premium.Id, Plan = premium, Status = ESubscriptionStatus.Active }));
        _jobs.CountActiveByAdvertiserAsync(Arg.Any<Guid>()).Returns(7);

        var result = await _useCase.ExecuteAsync(new CheckJobLimitRequest(Guid.NewGuid()));

        result.Data!.CanCreate.Should().BeTrue();
        result.Data.RemainingSlots.Should().Be(3);
    }

    [Fact]
    public async Task MissingPlanCatalog_DoesNotBlockAdvertisers()
    {
        // Banco sem planos cadastrados não pode travar a publicação de todos
        _plans.GetAllAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(RequestResult<IEnumerable<SubscriptionPlan>>.Success([]));
        _jobs.CountActiveByAdvertiserAsync(Arg.Any<Guid>()).Returns(50);

        var result = await _useCase.ExecuteAsync(new CheckJobLimitRequest(Guid.NewGuid()));

        result.Data!.CanCreate.Should().BeTrue();
        result.Data.MaxJobs.Should().Be(-1);
    }
}
