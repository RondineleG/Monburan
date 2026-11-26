using System.Net;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

public class LocalAdsApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly MonburanApiFactory _factory;
    private readonly HttpClient _client;

    public LocalAdsApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Click_ForAnActiveCampaign_RedirectsToTheTargetWithoutReferrer()
    {
        var campaign = await SeedCampaignAsync("https://anunciante.com.br/vagas");

        var response = await _client.GetAsync($"/ads/{campaign.Id.Value}/click");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.Should().Be("https://anunciante.com.br/vagas");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
        response.Headers.CacheControl?.NoStore.Should().BeTrue();

        await using var context = await NewContextAsync();
        (await context.LocalAdCampaigns.FindAsync(campaign.Id))!.ClickCount.Should().Be(1);
    }

    [Fact]
    public async Task Click_ForAnUnknownCampaign_Returns404()
    {
        var response = await _client.GetAsync($"/ads/{Guid.NewGuid()}/click");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Click_ForACampaignWithAnInsecureTarget_Returns404()
    {
        var campaign = await SeedCampaignAsync("http://anunciante.com.br/vagas");

        var response = await _client.GetAsync($"/ads/{campaign.Id.Value}/click");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Click_WhenTheFeatureFlagIsOff_Returns404AndDoesNotCount()
    {
        var campaign = await SeedCampaignAsync("https://anunciante.com.br/vagas");
        var flag = await SetLocalAdsFlagAsync(false);

        var response = await _client.GetAsync($"/ads/{campaign.Id.Value}/click");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await using var context = await NewContextAsync();
        (await context.LocalAdCampaigns.FindAsync(campaign.Id))!.ClickCount.Should().Be(0);
    }

    private async Task<LocalAdCampaign> SeedCampaignAsync(string targetUrl)
    {
        await SetLocalAdsFlagAsync(true);
        var campaign = new LocalAdCampaign
        {
            Id = Id.Create(Guid.NewGuid()),
            TenantId = "default",
            SponsorName = "Padaria Central",
            Headline = "Padaria Central contrata",
            Description = "Vagas na região.",
            TargetUrl = targetUrl,
            TargetLocationsJson = """[{"city":"Curitiba","state":"PR"}]""",
            TargetStates = "|PR|",
            StartsAt = DateTime.UtcNow.AddDays(-1),
            EndsAt = DateTime.UtcNow.AddDays(10),
            IsActive = true
        };
        await using var context = await NewContextAsync();
        context.LocalAdCampaigns.Add(campaign);
        await context.SaveChangesAsync();
        return campaign;
    }

    private async Task<FeatureFlag> SetLocalAdsFlagAsync(bool enabledForEveryone)
    {
        await using var context = await NewContextAsync();
        var flag = await context.FeatureFlags.FirstOrDefaultAsync(item => item.Name == LocalAdCampaignService.FeatureName);
        if (flag is null)
        {
            flag = new FeatureFlag
            {
                Id = Id.Create(Guid.NewGuid()),
                TenantId = "default",
                Name = LocalAdCampaignService.FeatureName,
                Description = "Publicidade local"
            };
            context.FeatureFlags.Add(flag);
        }

        flag.EnabledForEveryone = enabledForEveryone;
        await context.SaveChangesAsync();
        ((FeatureFlagService)_factory.Services.GetRequiredService<IFeatureFlags>()).Invalidate();
        return flag;
    }

    private Task<ApplicationDbContext> NewContextAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
}
