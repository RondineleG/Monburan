using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Web.Data;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

public class BillingApiTests : IClassFixture<MonburanBillingApiFactory>
{
    private readonly MonburanBillingApiFactory _factory;
    private readonly HttpClient _client;

    public BillingApiTests(MonburanBillingApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Checkout_WithoutAuthentication_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/subscriptions/checkout",
            new CreateSubscriptionCheckoutRequest { PlanId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_WithACandidateAccount_AndAnAdvertiserPlan_IsRefused()
    {
        var token = await RegisterAsync("candidate", "candidate");
        // /api/v1/plans anônimo retorna os planos de empresa — exatamente o que o
        // candidato não pode contratar (audiência cruzada).
        var plans = await _client.GetFromJsonAsync<AvailablePlansResponse>("/api/v1/plans");
        var advertiserPlan = plans!.Plans.First(p => p.Price > 0);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/subscriptions/checkout", token.AccessToken,
            new CreateSubscriptionCheckoutRequest { PlanId = advertiserPlan.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Checkout_WithACandidateAccount_AndACandidatePlan_ReturnsACheckout()
    {
        var token = await RegisterAsync("candidate", "candidate-flow");

        using var plansRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/plans");
        plansRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var plans = await (await _client.SendAsync(plansRequest))
            .Content.ReadFromJsonAsync<AvailablePlansResponse>();
        var candidatePlan = plans!.Plans.First(p => p.Price > 0);

        _factory.MercadoPago.EnqueueJson(HttpStatusCode.Created,
            """{"id":"PRE-C1","init_point":"https://www.mercadopago.com.br/subscriptions/checkout?preapproval_id=PRE-C1"}""");

        var checkout = await SendAsync(HttpMethod.Post, "/api/v1/subscriptions/checkout", token.AccessToken,
            new CreateSubscriptionCheckoutRequest { PlanId = candidatePlan.Id });

        checkout.StatusCode.Should().Be(HttpStatusCode.OK);
        var init = await checkout.Content.ReadFromJsonAsync<MercadoPagoCheckoutResponse>();
        init.Should().NotBeNull();
        await SubscriptionStatusIsAsync(init!.SubscriptionId, ESubscriptionStatus.Pending);
    }

    [Fact]
    public async Task Checkout_WithAnUnknownPlan_IsRefused()
    {
        var token = await RegisterAsync("advertiser", "no-plan");

        var response = await SendAsync(HttpMethod.Post, "/api/v1/subscriptions/checkout", token.AccessToken,
            new CreateSubscriptionCheckoutRequest { PlanId = Guid.NewGuid() });

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Checkout_ThenApprovedPaymentWebhook_ActivatesTheSubscription()
    {
        var token = await RegisterAsync("advertiser", "full-flow");
        var plan = await PaidPlanAsync();

        _factory.MercadoPago.EnqueueJson(HttpStatusCode.Created,
            """{"id":"PRE-1","init_point":"https://www.mercadopago.com.br/subscriptions/checkout?preapproval_id=PRE-1"}""");

        var checkout = await SendAsync(HttpMethod.Post, "/api/v1/subscriptions/checkout", token.AccessToken,
            new CreateSubscriptionCheckoutRequest { PlanId = plan.Id });

        var checkoutBody = await checkout.Content.ReadAsStringAsync();
        checkout.StatusCode.Should().Be(HttpStatusCode.OK, checkoutBody);
        var init = await checkout.Content.ReadFromJsonAsync<MercadoPagoCheckoutResponse>();
        init.Should().NotBeNull();
        init!.InitPoint.Should().StartWith("https://");
        await SubscriptionStatusIsAsync(init.SubscriptionId, ESubscriptionStatus.Pending);

        var price = plan.Price.ToString(CultureInfo.InvariantCulture);
        _factory.MercadoPago.EnqueueJson(HttpStatusCode.OK,
            $"{{\"id\":\"PAY-1\",\"status\":\"approved\",\"preapproval_id\":\"PRE-1\",\"transaction_amount\":{price},\"currency_id\":\"BRL\"}}");
        _factory.MercadoPago.EnqueueJson(HttpStatusCode.OK,
            $"{{\"id\":\"PRE-1\",\"external_reference\":\"{init.SubscriptionId}\",\"status\":\"authorized\"}}");

        var payload = """{"type":"subscription_authorized_payment","data":{"id":"PAY-1"}}""";
        using var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/mercadopago?data.id=PAY-1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add("x-signature", Sign("PAY-1"));
        webhook.Headers.Add("x-request-id", "req-1");

        var response = await _client.SendAsync(webhook);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await SubscriptionStatusIsAsync(init.SubscriptionId, ESubscriptionStatus.Active);
    }

    [Fact]
    public async Task Webhook_WithoutASignature_Returns503()
    {
        var payload = """{"type":"subscription_preapproval","data":{"id":"PRE-1"}}""";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/mercadopago?data.id=PRE-1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Webhook_WithAForgedSignature_Returns503()
    {
        var payload = """{"type":"subscription_preapproval","data":{"id":"PRE-1"}}""";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/mercadopago?data.id=PRE-1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-signature", "ts=1,v1=forjada");
        request.Headers.Add("x-request-id", "req-1");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Webhook_WithABodyOver64Kb_Returns413()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/mercadopago")
        {
            Content = new ByteArrayContent(new byte[64 * 1024 + 1])
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Webhook_WithAnUnrelatedSignedEvent_IsAcknowledged()
    {
        var payload = """{"type":"point_integration_whatsapp","data":{"id":"1"}}""";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/mercadopago?data.id=1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-signature", Sign("1"));
        request.Headers.Add("x-request-id", "req-1");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.MercadoPago.Requests.Should().BeEmpty();
    }

    private static string Sign(string dataId)
    {
        const string ts = "1762966800";
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:req-1;ts:{ts};";
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(MonburanBillingApiFactory.WebhookSecret), Encoding.UTF8.GetBytes(manifest)));
        return $"ts={ts},v1={hash}";
    }

    private async Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string url, string token, T body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<UserLoginResponse> RegisterAsync(string accountType, string tag)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = accountType,
            Name = $"Empresa {tag}",
            Email = $"{accountType}.{tag}.{Guid.NewGuid():N}@teste.com",
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!;
    }

    private async Task<PlanResponse> PaidPlanAsync()
    {
        var plans = await _client.GetFromJsonAsync<AvailablePlansResponse>("/api/v1/plans");
        return plans!.Plans.First(p => p.Price > 0);
    }

    private async Task SubscriptionStatusIsAsync(Guid id, ESubscriptionStatus expected)
    {
        await using var context = await _factory.Services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();
        var subscription = await context.Subscriptions.SingleAsync(item => item.Id.Value == id);
        subscription.Status.Should().Be(expected);
    }
}
