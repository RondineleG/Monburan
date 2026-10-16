using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

public class SubscriptionsApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public SubscriptionsApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CancelSubscription_FromOtherAdvertiser_Returns404()
    {
        // Anunciante B assina um plano pago
        var tokenB = await RegisterAdvertiserAsync("b");
        var plans = await _client.GetFromJsonAsync<AvailablePlansResponse>("/api/v1/plans");
        var paidPlan = plans!.Plans.First(p => p.Price > 0);

        using var upgradeReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/upgrade")
        {
            Content = JsonContent.Create(new UpgradePlanRequest
            {
                NewPlanId = paidPlan.Id,
                PaymentMethod = "card"
            })
        };
        upgradeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB.AccessToken);
        var upgrade = await _client.SendAsync(upgradeReq);
        upgrade.StatusCode.Should().Be(HttpStatusCode.OK);
        var subscription = await upgrade.Content.ReadFromJsonAsync<SubscriptionResponse>();

        // Anunciante A tenta cancelar a assinatura de B — deve falhar (IDOR)
        var tokenA = await RegisterAdvertiserAsync("a");
        using var cancelReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/cancel")
        {
            Content = JsonContent.Create(new CancelSubscriptionRequest
            {
                SubscriptionId = subscription!.Id
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
                SubscriptionId = subscription.Id,
                Reason = "teste"
            })
        };
        ownCancel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB.AccessToken);

        var ownResponse = await _client.SendAsync(ownCancel);
        ownResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Plans_WithAdvertiserBearer_ReturnsCurrentSubscription()
    {
        // /plans é AllowAnonymous: o bearer precisa ser autenticado explicitamente
        // para devolver CurrentSubscription (bug: DefaultScheme=cookie ignorava o JWT)
        var token = await RegisterAdvertiserAsync("plans");

        // Anunciante assina um plano — aí /plans deve refletir a assinatura dele
        var plans = await _client.GetFromJsonAsync<AvailablePlansResponse>("/api/v1/plans");
        var paidPlan = plans!.Plans.First(p => p.Price > 0);
        using var upgradeReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/upgrade")
        {
            Content = JsonContent.Create(new UpgradePlanRequest
            {
                NewPlanId = paidPlan.Id,
                PaymentMethod = "card"
            })
        };
        upgradeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        (await _client.SendAsync(upgradeReq)).StatusCode.Should().Be(HttpStatusCode.OK);

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
        var token = await RegisterAdvertiserAsync("plans-refresh");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/plans");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.RefreshToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AvailablePlansResponse>();
        body!.CurrentSubscription.Should().BeNull();
    }

    private async Task<UserLoginResponse> RegisterAdvertiserAsync(string tag)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "advertiser",
            Name = $"Empresa {tag}",
            Email = $"adv.{tag}.{Guid.NewGuid():N}@teste.com",
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!;
    }
}
