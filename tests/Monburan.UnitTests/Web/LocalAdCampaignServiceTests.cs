using System.Security.Claims;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Monburan.Core.Entities;
using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Services;
using NSubstitute;
using Xunit;

namespace Monburan.UnitTests.Web;

public class LocalAdCampaignServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestDbContextFactory _factory = new();
    private readonly IFeatureFlags _flags = Substitute.For<IFeatureFlags>();
    private readonly IAdminAuditLog _audit = Substitute.For<IAdminAuditLog>();
    private readonly LocalAdCampaignService _service;

    private static readonly ClaimsPrincipal Admin = new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Role, "Admin"), new Claim(ClaimTypes.NameIdentifier, "admin-1"),
         new Claim(ClaimTypes.Email, "admin@monburan.com.br")], "test"));

    private static readonly ClaimsPrincipal NotAdmin = new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Email, "user@x.com")], "test"));

    public LocalAdCampaignServiceTests()
    {
        _flags.IsEnabledAsync(LocalAdCampaignService.FeatureName, Arg.Any<ClaimsPrincipal?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _service = new LocalAdCampaignService(_factory, _flags, _audit, new FixedClock(Now));
    }

    public void Dispose()
    {
        using var context = _factory.CreateDbContext();
        context.Database.EnsureDeleted();
    }

    [Fact]
    public async Task List_ForSomeoneWhoIsNotAdmin_IsEmpty()
    {
        await SeedAsync(Campaign("Curitiba", "PR"));

        (await _service.ListAsync(NotAdmin)).Should().BeEmpty();
        (await _service.ListAsync(new ClaimsPrincipal())).Should().BeEmpty();
    }

    [Fact]
    public async Task List_WhenTheFlagIsOff_IsEmptyEvenForAdmin()
    {
        _flags.IsEnabledAsync(LocalAdCampaignService.FeatureName, Admin, Arg.Any<CancellationToken>()).Returns(false);
        await SeedAsync(Campaign("Curitiba", "PR"));

        (await _service.ListAsync(Admin)).Should().BeEmpty();
    }

    [Fact]
    public async Task Save_ForSomeoneWhoIsNotAdmin_IsRefused()
    {
        var result = await _service.SaveAsync(ValidInput(), NotAdmin);

        result.Status.Should().Be(EResultStatus.DomainError);
        (await CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("", "Título", "https://anunciante.com.br", "Curitiba, PR")]
    [InlineData("Anunciante", "", "https://anunciante.com.br", "Curitiba, PR")]
    [InlineData("Anunciante", "Título", "http://anunciante.com.br", "Curitiba, PR")]
    [InlineData("Anunciante", "Título", "https://anunciante.com.br", "")]
    [InlineData("Anunciante", "Título", "https://anunciante.com.br", "Curitiba")]
    [InlineData("Anunciante", "Título", "https://anunciante.com.br", "Curitiba, SP")]
    [InlineData("Anunciante", "Título", "https://anunciante.com.br", "Cidade Que Não Existe, PR")]
    public async Task Save_WithInvalidInput_IsRefused(string sponsor, string headline, string url, string locations)
    {
        var result = await _service.SaveAsync(ValidInput() with
        {
            SponsorName = sponsor, Headline = headline, TargetUrl = url, LocationsText = locations
        }, Admin);

        result.Status.Should().Be(EResultStatus.DomainError);
        (await CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Save_WithEndBeforeStart_IsRefused()
    {
        var result = await _service.SaveAsync(ValidInput() with
        {
            StartsAt = new DateTime(2026, 11, 20), EndsAt = new DateTime(2026, 11, 10)
        }, Admin);

        result.Status.Should().Be(EResultStatus.DomainError);
        (await CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Save_WithAnImageOutsideTheUploadsPath_IsRefused()
    {
        var result = await _service.SaveAsync(ValidInput() with
        {
            ImageUrl = "https://cdn.evil.com/banner.png"
        }, Admin);

        result.Status.Should().Be(EResultStatus.DomainError);
        (await CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Save_WithValidInput_PersistsTheCampaignAndAudits()
    {
        var result = await _service.SaveAsync(ValidInput(), Admin);

        result.Status.Should().Be(EResultStatus.Success);
        await using var context = _factory.CreateDbContext();
        var saved = await context.LocalAdCampaigns.SingleAsync();
        saved.SponsorName.Should().Be("Padaria Central");
        saved.TargetStates.Should().Be("|PR|");
        saved.StartsAt.Should().Be(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));
        saved.EndsAt.Should().Be(new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1));
        saved.IsActive.Should().BeTrue();
        await _audit.Received(1).RecordAsync(
            Arg.Is<AdminAuditRecord>(r => r.Action == AdminAuditActions.LocalAdCampaignSaved),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_MarksTheCampaignAsDeletedAndAudits()
    {
        var campaign = Campaign("Curitiba", "PR");
        await SeedAsync(campaign);

        var result = await _service.DeleteAsync(campaign.Id.Value, Admin);

        result.Status.Should().Be(EResultStatus.Success);
        await using var context = _factory.CreateDbContext();
        (await context.LocalAdCampaigns.SingleAsync()).IsDeleted.Should().BeTrue();
        (await _service.ListAsync(Admin)).Should().BeEmpty();
        await _audit.Received(1).RecordAsync(
            Arg.Is<AdminAuditRecord>(r => r.Action == AdminAuditActions.LocalAdCampaignDeleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetForJob_WhenTheFlagIsOff_ReturnsNothing()
    {
        _flags.IsEnabledAsync(LocalAdCampaignService.FeatureName, Arg.Any<ClaimsPrincipal?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var job = ActiveJob("Curitiba", "PR");
        await SeedAsync(job, Campaign("Curitiba", "PR"));

        (await _service.GetForJobAsync(job.Id.Value)).Should().BeNull();
    }

    [Fact]
    public async Task GetForJob_WithoutAMatchingCity_ReturnsNothing()
    {
        var job = ActiveJob("Curitiba", "PR");
        await SeedAsync(job, Campaign("Joinville", "SC"));

        (await _service.GetForJobAsync(job.Id.Value)).Should().BeNull();
    }

    [Fact]
    public async Task GetForJob_WithAMatchingCityAndState_ReturnsThePlacement()
    {
        var job = ActiveJob("Curitiba", "PR");
        var campaign = Campaign("Curitiba", "PR");
        await SeedAsync(job, campaign);

        var placement = await _service.GetForJobAsync(job.Id.Value);

        placement.Should().NotBeNull();
        placement!.Id.Should().Be(campaign.Id.Value);
        placement.Headline.Should().Be("Padaria Central contrata");
        placement.ClickUrl.Should().Be($"/ads/{campaign.Id.Value:D}/click");
    }

    private static Subscription PaidSubscription(Guid? candidateId = null, Guid? advertiserId = null, decimal price = 19.90m,
        ESubscriptionStatus status = ESubscriptionStatus.Active, DateTime? endDate = null)
    {
        var plan = new SubscriptionPlan { Name = "Qualquer nome", Price = price, TenantId = "default" };
        return new Subscription
        {
            TenantId = "default",
            CandidateId = candidateId is { } c ? Id.Create(c) : null,
            AdvertiserId = advertiserId is { } a ? Id.Create(a) : null,
            PlanId = plan.Id,
            Plan = plan,
            Status = status,
            EndDate = endDate ?? Now.AddDays(30)
        };
    }

    [Fact]
    public async Task GetForJob_PaidCandidate_SeesNoAd_RegardlessOfPlanName()
    {
        var job = ActiveJob("Curitiba", "PR");
        var candidateId = Guid.NewGuid();
        await SeedAsync(job, Campaign("Curitiba", "PR"), PaidSubscription(candidateId: candidateId));

        (await _service.GetForJobAsync(job.Id.Value, viewerCandidateId: candidateId)).Should().BeNull();
    }

    [Fact]
    public async Task GetForJob_PaidCompany_SeesNoAd()
    {
        var job = ActiveJob("Curitiba", "PR");
        var advertiserId = Guid.NewGuid();
        await SeedAsync(job, Campaign("Curitiba", "PR"), PaidSubscription(advertiserId: advertiserId, price: 99m));

        (await _service.GetForJobAsync(job.Id.Value, viewerAdvertiserId: advertiserId)).Should().BeNull();
    }

    [Theory]
    [InlineData(0, ESubscriptionStatus.Active, 30)]        // plano gratuito
    [InlineData(19.90, ESubscriptionStatus.Cancelled, 30)] // cancelada
    [InlineData(19.90, ESubscriptionStatus.Pending, 30)]   // pagamento não confirmado
    [InlineData(19.90, ESubscriptionStatus.Active, -1)]    // vigência acabou
    public async Task GetForJob_WithoutAnActivePaidPlan_StillSeesTheAd(double price, ESubscriptionStatus status, int endsInDays)
    {
        var job = ActiveJob("Curitiba", "PR");
        var candidateId = Guid.NewGuid();
        await SeedAsync(job, Campaign("Curitiba", "PR"),
            PaidSubscription(candidateId: candidateId, price: (decimal)price, status: status, endDate: Now.AddDays(endsInDays)));

        (await _service.GetForJobAsync(job.Id.Value, viewerCandidateId: candidateId)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetForJob_MatchesTheCityRegardlessOfAccentsAndCase()
    {
        var job = ActiveJob("SÃO JOSE DOS PINHAIS", "pr");
        var campaign = Campaign("São José dos Pinhais", "PR");
        await SeedAsync(job, campaign);

        (await _service.GetForJobAsync(job.Id.Value)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetForJob_SkipsCampaignsOutsideTheirWindowOrPaused()
    {
        var job = ActiveJob("Curitiba", "PR");
        var paused = Campaign("Curitiba", "PR");
        paused.IsActive = false;
        var expired = Campaign("Curitiba", "PR");
        expired.EndsAt = Now.AddDays(-1);
        var future = Campaign("Curitiba", "PR");
        future.StartsAt = Now.AddDays(10);
        await SeedAsync(job, paused, expired, future);

        (await _service.GetForJobAsync(job.Id.Value)).Should().BeNull();
    }

    [Fact]
    public async Task RegisterImpression_IncrementsTheCounterOnlyWithinTheWindow()
    {
        var campaign = Campaign("Curitiba", "PR");
        var expired = Campaign("Curitiba", "PR");
        expired.EndsAt = Now.AddDays(-1);
        await SeedAsync(campaign, expired);

        await _service.RegisterImpressionAsync(campaign.Id.Value);
        await _service.RegisterImpressionAsync(campaign.Id.Value);
        await _service.RegisterImpressionAsync(expired.Id.Value);

        await using var context = _factory.CreateDbContext();
        (await context.LocalAdCampaigns.FindAsync(campaign.Id))!.ImpressionCount.Should().Be(2);
        (await context.LocalAdCampaigns.FindAsync(expired.Id))!.ImpressionCount.Should().Be(0);
    }

    [Fact]
    public async Task RegisterClick_CountsAndReturnsTheTargetUrl()
    {
        var campaign = Campaign("Curitiba", "PR");
        await SeedAsync(campaign);

        var target = await _service.RegisterClickAndGetTargetAsync(campaign.Id.Value);

        target.Should().Be("https://padariacentral.com.br/vagas");
        await using var context = _factory.CreateDbContext();
        (await context.LocalAdCampaigns.FindAsync(campaign.Id))!.ClickCount.Should().Be(1);
    }

    [Fact]
    public async Task RegisterClick_ForAnUnknownOrIneligibleCampaign_ReturnsNull()
    {
        var paused = Campaign("Curitiba", "PR");
        paused.IsActive = false;
        await SeedAsync(paused);

        (await _service.RegisterClickAndGetTargetAsync(paused.Id.Value)).Should().BeNull();
        (await _service.RegisterClickAndGetTargetAsync(Guid.NewGuid())).Should().BeNull();
    }

    private async Task<int> CountAsync()
    {
        await using var context = _factory.CreateDbContext();
        return await context.LocalAdCampaigns.CountAsync();
    }

    private async Task SeedAsync(params BaseEntity[] entities)
    {
        await using var context = _factory.CreateDbContext();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    private static LocalAdCampaign Campaign(string city, string state) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = "default",
        SponsorName = "Padaria Central",
        Headline = "Padaria Central contrata",
        Description = "Vagas para atendente e padeiro.",
        TargetUrl = "https://padariacentral.com.br/vagas",
        TargetLocationsJson = $"[{{\"city\":\"{city}\",\"state\":\"{state}\"}}]",
        TargetStates = $"|{state}|",
        StartsAt = Now.AddDays(-1),
        EndsAt = Now.AddDays(10),
        IsActive = true
    };

    private static Job ActiveJob(string city, string state) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = "default",
        Title = "Atendente",
        Status = EJobStatus.Active,
        Location = new JobLocation { City = city, State = state }
    };

    private static LocalAdCampaignInput ValidInput() => new(
        Id: null,
        SponsorName: "Padaria Central",
        Headline: "Padaria Central contrata",
        Description: "Vagas para atendente.",
        ImageUrl: null,
        TargetUrl: "https://padariacentral.com.br/vagas",
        LocationsText: "Curitiba, PR",
        StartsAt: new DateTime(2026, 11, 10),
        EndsAt: new DateTime(2026, 11, 20),
        IsActive: true);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
