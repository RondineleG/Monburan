using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.ApiTests;

public class SubscriptionsApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly MonburanApiFactory _factory;
    private readonly HttpClient _client;

    public SubscriptionsApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CancelSubscription_FromOtherAdvertiser_Returns404()
    {
        // Anunciante B tem uma assinatura paga ativa (ativada fora do endpoint,
        // como fica após a confirmação do webhook de pagamento)
        var (tokenB, emailB) = await RegisterAdvertiserAsync("b");
        var paidPlan = await PaidPlanAsync();
        var subscriptionId = await SeedActiveSubscriptionAsync(emailB, paidPlan.Id);

        // Anunciante A tenta cancelar a assinatura de B — deve falhar (IDOR)
        var (tokenA, _) = await RegisterAdvertiserAsync("a");
        using var cancelReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/cancel")
        {
            Content = JsonContent.Create(new CancelSubscriptionRequest
            {
                SubscriptionId = subscriptionId
            })
        };
        cancelReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA.AccessToken);

        var cancel = await _client.SendAsync(cancelReq);

        cancel.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // E a assinatura de B continua cancelável por B (controle positivo)
        using var ownCancel = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/cancel")
        {
            Content = JsonContent.Create(new CancelSubscriptionRequest
            {
                SubscriptionId = subscriptionId,
                Reason = "teste"
            })
        };
        ownCancel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB.AccessToken);

        var ownResponse = await _client.SendAsync(ownCancel);
        ownResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpgradePlan_ToAPaidPlan_IsRefused_MustGoThroughCheckout()
    {
        // Plano pago nunca é ativado pelo upgrade manual: só via checkout + webhook do gateway
        var (token, _) = await RegisterAdvertiserAsync("paid-upgrade");
        var paidPlan = await PaidPlanAsync();

        using var upgradeReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/upgrade")
        {
            Content = JsonContent.Create(new UpgradePlanRequest
            {
                NewPlanId = paidPlan.Id,
                PaymentMethod = "card"
            })
        };
        upgradeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(upgradeReq);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Plans_WithAdvertiserBearer_ReturnsCurrentSubscription()
    {
        // /plans é AllowAnonymous: o bearer precisa ser autenticado explicitamente
        // para devolver CurrentSubscription (bug: DefaultScheme=cookie ignorava o JWT)
        var (token, email) = await RegisterAdvertiserAsync("plans");

        // Anunciante com assinatura paga ativa — aí /plans deve refletir a assinatura dele
        var paidPlan = await PaidPlanAsync();
        await SeedActiveSubscriptionAsync(email, paidPlan.Id);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/plans");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AvailablePlansResponse>();
        body!.CurrentSubscription.Should().NotBeNull(
            "anunciante autenticado deve receber a assinatura atual");
        body.CurrentSubscription!.PlanName.Should().Be(paidPlan.Name);
    }

    [Fact]
    public async Task Plans_WithRefreshToken_DoesNotExposeCurrentSubscription()
    {
        // Refresh token não pode identificar perfil nem mesmo no endpoint anônimo
        var (token, _) = await RegisterAdvertiserAsync("plans-refresh");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/plans");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.RefreshToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AvailablePlansResponse>();
        body!.CurrentSubscription.Should().BeNull();
    }

    private async Task<(UserLoginResponse Token, string Email)> RegisterAdvertiserAsync(string tag)
    {
        var email = $"adv.{tag}.{Guid.NewGuid():N}@teste.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "advertiser",
            Name = $"Empresa {tag}",
            Email = email,
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<UserLoginResponse>())!, email);
    }

    private async Task<PlanResponse> PaidPlanAsync()
    {
        var plans = await _client.GetFromJsonAsync<AvailablePlansResponse>("/api/v1/plans");
        return plans!.Plans.First(p => p.Price > 0);
    }

    /// <summary>Cria a assinatura paga ativa como ela fica depois do webhook confirmar o pagamento.</summary>
    private async Task<Guid> SeedActiveSubscriptionAsync(string advertiserEmail, Guid planId)
    {
        await using var context = await _factory.Services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();
        var advertiser = await context.Advertisers.SingleAsync(item => item.Email == advertiserEmail);
        var plan = await context.SubscriptionPlans.SingleAsync(item => item.Id.Value == planId);
        var subscription = new Subscription
        {
            Id = Id.Create(Guid.NewGuid()),
            TenantId = "default",
            AdvertiserId = advertiser.Id,
            PlanId = plan.Id,
            StartDate = DateTime.UtcNow,
            Status = ESubscriptionStatus.Active,
            PaymentId = "manual_card"
        };
        advertiser.ActivePlan = plan.Name;
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();
        return subscription.Id.Value;
    }
}
