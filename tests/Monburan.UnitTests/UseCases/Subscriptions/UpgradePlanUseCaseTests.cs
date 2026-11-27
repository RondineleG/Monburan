using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.ValueObjects;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Subscriptions;

public class UpgradePlanUseCaseTests
{
    private readonly IBaseRepository<Advertiser> _advertisers = Substitute.For<IBaseRepository<Advertiser>>();
    private readonly IBaseRepository<SubscriptionPlan> _plans = Substitute.For<IBaseRepository<SubscriptionPlan>>();
    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly UpgradePlanUseCase _useCase;

    public UpgradePlanUseCaseTests()
    {
        _advertisers.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Advertiser>.Success(new Advertiser()));
        _useCase = new UpgradePlanUseCase(_advertisers, _plans, _subscriptions);
    }

    private void GivenPlan(decimal price) =>
        _plans.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(RequestResult<SubscriptionPlan>.Success(
            new SubscriptionPlan { Name = "Premium", Price = price, IsActive = true, Audience = EPlanAudience.Advertiser }));

    private static UpgradePlanRequest Request() => new()
    {
        AdvertiserId = Guid.NewGuid(),
        NewPlanId = Guid.NewGuid(),
        PaymentMethod = "manual"
    };

    [Fact]
    public async Task PaidPlan_IsNeverActivatedThroughUpgradeEndpoint()
    {
        GivenPlan(price: 99.9m);

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        await _subscriptions.DidNotReceiveWithAnyArgs().GetActiveByAdvertiserIdAsync(default!, default);
        await _subscriptions.DidNotReceiveWithAnyArgs().ChangePlanAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task FreePlan_CanStillUseTheUpgradeEndpoint()
    {
        GivenPlan(price: 0m);
        _subscriptions.GetActiveByAdvertiserIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Subscription?>.WithError("parou aqui de propósito"));

        await _useCase.ExecuteAsync(Request());

        await _subscriptions.Received(1).GetActiveByAdvertiserIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>());
    }
}
