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

public class CheckResumeLimitUseCaseTests
{
    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly IBaseRepository<SubscriptionPlan> _plans = Substitute.For<IBaseRepository<SubscriptionPlan>>();
    private readonly IResumeRepository _resumes = Substitute.For<IResumeRepository>();
    private readonly CheckResumeLimitUseCase _useCase;

    private static readonly SubscriptionPlan CandidateFree = new() { Name = "Free", Price = 0, MaxResumes = 3, Audience = EPlanAudience.Candidate, IsActive = true };
    private static readonly SubscriptionPlan AdvertiserFree = new() { Name = "Free", Price = 0, MaxResumes = 5, Audience = EPlanAudience.Advertiser, IsActive = true };

    public CheckResumeLimitUseCaseTests()
    {
        _subscriptions.GetActiveByCandidateIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(RequestResult<Subscription?>.Success(null));
        _plans.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<IEnumerable<SubscriptionPlan>>.Success([AdvertiserFree, CandidateFree]));
        _useCase = new CheckResumeLimitUseCase(_subscriptions, _plans, _resumes);
    }

    private void Count(int resumes) =>
        _resumes.CountByCandidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(RequestResult<int>.Success(resumes));

    [Fact]
    public async Task WithoutSubscription_UsesTheCandidateFreePlan_NotTheCompanyOne()
    {
        Count(3);

        var result = await _useCase.ExecuteAsync(Guid.NewGuid());

        result.Data!.CanCreate.Should().BeFalse();
        result.Data.MaxResumes.Should().Be(3);
    }

    [Fact]
    public async Task BelowTheLimit_CanCreate()
    {
        Count(2);

        (await _useCase.ExecuteAsync(Guid.NewGuid())).Data!.CanCreate.Should().BeTrue();
    }

    [Fact]
    public async Task UnlimitedPaidPlan_AlwaysCanCreate()
    {
        var premium = new SubscriptionPlan { Name = "Premium", Price = 9.90m, MaxResumes = -1, Audience = EPlanAudience.Candidate };
        _subscriptions.GetActiveByCandidateIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Subscription?>.Success(new Subscription { PlanId = premium.Id, Plan = premium, Status = ESubscriptionStatus.Active }));
        Count(40);

        (await _useCase.ExecuteAsync(Guid.NewGuid())).Data!.CanCreate.Should().BeTrue();
    }

    [Fact]
    public async Task MissingPlanCatalog_DoesNotBlockAnyone()
    {
        _plans.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<IEnumerable<SubscriptionPlan>>.Success([]));
        Count(100);

        (await _useCase.ExecuteAsync(Guid.NewGuid())).Data!.CanCreate.Should().BeTrue();
    }
}
