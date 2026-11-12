using System.Net;
using System.Security.Cryptography;
using System.Text;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monburan.Core.Entities;
using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Services;
using NSubstitute;
using Xunit;

namespace Monburan.UnitTests.Web;

public class MercadoPagoBillingServiceTests : IDisposable
{
    private const string Secret = "segredo-do-webhook";
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestDbContextFactory _factory = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly MercadoPagoOptions _options = new()
    {
        AccessToken = "APP-TEST",
        WebhookSecret = Secret,
        PublicBaseUrl = "https://monburan.com.br"
    };
    private readonly MercadoPagoBillingService _service;

    public MercadoPagoBillingServiceTests()
    {
        var options = Substitute.For<IOptionsMonitor<MercadoPagoOptions>>();
        options.CurrentValue.Returns(_ => _options);
        _service = new MercadoPagoBillingService(
            new HttpClient(_handler), _factory, options, new FixedClock(Now),
            Substitute.For<ILogger<MercadoPagoBillingService>>());
    }

    public void Dispose()
    {
        using var context = _factory.CreateDbContext();
        context.Database.EnsureDeleted();
    }

    [Theory]
    [InlineData("", Secret, "https://monburan.com.br")]
    [InlineData("APP-TEST", "", "https://monburan.com.br")]
    [InlineData("APP-TEST", Secret, "http://monburan.com.br")]
    [InlineData("APP-TEST", Secret, "")]
    public void IsConfigured_RequiresBothCredentialsAndAnHttpsBaseUrl(string token, string secret, string baseUrl)
    {
        _options.AccessToken = token;
        _options.WebhookSecret = secret;
        _options.PublicBaseUrl = baseUrl;

        _service.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_WithCredentialsAndHttpsBaseUrl_IsTrue()
    {
        _service.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Checkout_WithoutCredentials_IsRefused()
    {
        _options.AccessToken = "";

        var result = await _service.CreateCheckoutAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Status.Should().Be(EResultStatus.DomainError);
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Checkout_ForAnUnknownAdvertiser_IsRefused()
    {
        var result = await _service.CreateCheckoutAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Status.Should().Be(EResultStatus.DomainError);
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Checkout_ForAFreeOrCandidatePlan_IsRefused()
    {
        var advertiser = NewAdvertiser();
        var candidatePlan = NewPlan();
        candidatePlan.Audience = EPlanAudience.Candidate;
        var freePlan = NewPlan();
        freePlan.Price = 0;
        await SeedAsync(advertiser, candidatePlan, freePlan);

        (await _service.CreateCheckoutAsync(advertiser.Id.Value, candidatePlan.Id.Value)).Status
            .Should().Be(EResultStatus.DomainError);
        (await _service.CreateCheckoutAsync(advertiser.Id.Value, freePlan.Id.Value)).Status
            .Should().Be(EResultStatus.DomainError);
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Checkout_WhenThePlanIsAlreadyActive_IsRefused()
    {
        var advertiser = NewAdvertiser();
        var plan = NewPlan();
        var active = NewSubscription(advertiser, plan);
        active.Status = ESubscriptionStatus.Active;
        await SeedAsync(advertiser, plan, active);

        var result = await _service.CreateCheckoutAsync(advertiser.Id.Value, plan.Id.Value);

        result.Status.Should().Be(EResultStatus.DomainError);
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Checkout_CreatesAPendingSubscription_AndReturnsTheProviderUrl()
    {
        var advertiser = NewAdvertiser();
        var plan = NewPlan();
        await SeedAsync(advertiser, plan);
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.Created,
            """{"id":"PRE-1","init_point":"https://www.mercadopago.com.br/subscriptions/checkout?preapproval_id=PRE-1"}""")));

        var result = await _service.CreateCheckoutAsync(advertiser.Id.Value, plan.Id.Value);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.InitPoint.Should().Contain("mercadopago.com.br");
        await using var context = _factory.CreateDbContext();
        var pending = await context.Subscriptions.SingleAsync();
        pending.Status.Should().Be(ESubscriptionStatus.Pending);
        pending.PaymentId.Should().Be("mp-preapproval:PRE-1");
        pending.AdvertiserId.Should().Be(advertiser.Id);

        var sent = _handler.Requests.Should().ContainSingle().Subject;
        sent.Method.Should().Be(HttpMethod.Post);
        sent.RequestUri!.Host.Should().Be("api.mercadopago.com");
        sent.Headers.GetValues("X-Idempotency-Key").Should().ContainSingle().Which.Should().Be(pending.Id.Value.ToString());
    }

    [Fact]
    public async Task Checkout_WhenTheProviderRefuses_ExpiresThePendingSubscription()
    {
        var advertiser = NewAdvertiser();
        var plan = NewPlan();
        await SeedAsync(advertiser, plan);
        _handler.Enqueue(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)));

        var result = await _service.CreateCheckoutAsync(advertiser.Id.Value, plan.Id.Value);

        result.Status.Should().Be(EResultStatus.DomainError);
        await using var context = _factory.CreateDbContext();
        (await context.Subscriptions.SingleAsync()).Status.Should().Be(ESubscriptionStatus.Expired);
    }

    [Fact]
    public async Task Checkout_WhenTheProviderReturnsAnUnsafeUrl_ExpiresThePendingSubscription()
    {
        var advertiser = NewAdvertiser();
        var plan = NewPlan();
        await SeedAsync(advertiser, plan);
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.Created,
            """{"id":"PRE-1","init_point":"https://evil.example.com/checkout"}""")));

        var result = await _service.CreateCheckoutAsync(advertiser.Id.Value, plan.Id.Value);

        result.Status.Should().Be(EResultStatus.DomainError);
        await using var context = _factory.CreateDbContext();
        (await context.Subscriptions.SingleAsync()).Status.Should().Be(ESubscriptionStatus.Expired);
    }

    [Fact]
    public async Task Checkout_OnNetworkFailure_ExpiresThePendingSubscription()
    {
        var advertiser = NewAdvertiser();
        var plan = NewPlan();
        await SeedAsync(advertiser, plan);
        _handler.Enqueue(_ => throw new HttpRequestException("sem rede"));

        var result = await _service.CreateCheckoutAsync(advertiser.Id.Value, plan.Id.Value);

        result.Status.Should().Be(EResultStatus.DomainError);
        await using var context = _factory.CreateDbContext();
        (await context.Subscriptions.SingleAsync()).Status.Should().Be(ESubscriptionStatus.Expired);
    }

    [Fact]
    public async Task Webhook_WithoutAValidSignature_IsNotHandled()
    {
        var handled = await _service.HandleWebhookAsync(
            """{"type":"subscription_preapproval","data":{"id":"PRE-1"}}""",
            "ts=1,v1=assinatura-invalida", "req-1", "PRE-1");

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_WithoutDataId_IsNotHandled()
    {
        var handled = await _service.HandleWebhookAsync(
            """{"type":"subscription_preapproval"}""", null, null, null);

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_WithAnUnrelatedEventType_IsAcknowledgedWithoutWork()
    {
        var handled = await _service.HandleWebhookAsync(
            """{"type":"payment.created","data":{"id":"1"}}""",
            Sign("1"), "req-1", "1");

        handled.Should().BeTrue();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Webhook_AuthorizingTheMandate_DoesNotActivateTheSubscription()
    {
        var advertiser = NewAdvertiser();
        var plan = NewPlan();
        var subscription = NewSubscription(advertiser, plan);
        await SeedAsync(advertiser, plan, subscription);
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.OK,
            $"{{\"id\":\"PRE-1\",\"external_reference\":\"{subscription.Id.Value}\",\"status\":\"authorized\"}}")));

        var handled = await _service.HandleWebhookAsync(
            """{"type":"subscription_preapproval","data":{"id":"PRE-1"}}""",
            Sign("PRE-1"), "req-1", "PRE-1");

        handled.Should().BeTrue();
        await using var context = _factory.CreateDbContext();
        var stored = await context.Subscriptions.Include(s => s.Advertiser).SingleAsync();
        stored.Status.Should().Be(ESubscriptionStatus.Pending);
        stored.Advertiser!.ActivePlan.Should().Be("Free");
    }

    [Fact]
    public async Task Webhook_AnApprovedPayment_ActivatesTheSubscriptionAndThePlan()
    {
        var advertiser = NewAdvertiser();
        var plan = NewPlan();
        var subscription = NewSubscription(advertiser, plan);
        await SeedAsync(advertiser, plan, subscription);
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.OK,
            $"{{\"id\":\"PAY-1\",\"status\":\"approved\",\"preapproval_id\":\"PRE-1\",\"transaction_amount\":{plan.Price},\"currency_id\":\"BRL\"}}")));
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.OK,
            $"{{\"id\":\"PRE-1\",\"external_reference\":\"{subscription.Id.Value}\",\"status\":\"authorized\"}}")));

        var handled = await _service.HandleWebhookAsync(
            """{"type":"subscription_authorized_payment","data":{"id":"PAY-1"}}""",
            Sign("PAY-1"), "req-1", "PAY-1");

        handled.Should().BeTrue();
        await using var context = _factory.CreateDbContext();
        var stored = await context.Subscriptions.Include(s => s.Advertiser).SingleAsync();
        stored.Status.Should().Be(ESubscriptionStatus.Active);
        stored.EndDate.Should().BeNull();
        stored.PaymentId.Should().Be("mp-preapproval:PRE-1");
        stored.Advertiser!.ActivePlan.Should().Be(plan.Name);
    }

    [Fact]
    public async Task Webhook_APaymentWithTheWrongAmount_IsRejected()
    {
        var advertiser = NewAdvertiser();
        var plan = NewPlan();
        var subscription = NewSubscription(advertiser, plan);
        await SeedAsync(advertiser, plan, subscription);
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.OK,
            """{"id":"PAY-1","status":"approved","preapproval_id":"PRE-1","transaction_amount":1.00,"currency_id":"BRL"}""")));
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.OK,
            $"{{\"id\":\"PRE-1\",\"external_reference\":\"{subscription.Id.Value}\",\"status\":\"authorized\"}}")));

        var handled = await _service.HandleWebhookAsync(
            """{"type":"subscription_authorized_payment","data":{"id":"PAY-1"}}""",
            Sign("PAY-1"), "req-1", "PAY-1");

        handled.Should().BeFalse();
        await using var context = _factory.CreateDbContext();
        (await context.Subscriptions.SingleAsync()).Status.Should().Be(ESubscriptionStatus.Pending);
    }

    [Fact]
    public async Task Webhook_APaymentForAnExpiredCheckout_CancelsItAtTheProvider()
    {
        var advertiser = NewAdvertiser();
        var plan = NewPlan();
        var subscription = NewSubscription(advertiser, plan);
        subscription.Status = ESubscriptionStatus.Expired;
        subscription.EndDate = Now.AddDays(-1);
        subscription.PaymentId = "mp-preapproval:PRE-1";
        await SeedAsync(advertiser, plan, subscription);
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.OK,
            $"{{\"id\":\"PAY-1\",\"status\":\"approved\",\"preapproval_id\":\"PRE-1\",\"transaction_amount\":{plan.Price},\"currency_id\":\"BRL\"}}")));
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.OK,
            $"{{\"id\":\"PRE-1\",\"external_reference\":\"{subscription.Id.Value}\",\"status\":\"authorized\"}}")));
        _handler.Enqueue(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var handled = await _service.HandleWebhookAsync(
            """{"type":"subscription_authorized_payment","data":{"id":"PAY-1"}}""",
            Sign("PAY-1"), "req-1", "PAY-1");

        handled.Should().BeTrue();
        _handler.Requests.Should().Contain(r => r.Method == HttpMethod.Put
            && r.RequestUri!.AbsolutePath.EndsWith("/preapproval/PRE-1"));
        await using var context = _factory.CreateDbContext();
        (await context.Subscriptions.SingleAsync()).Status.Should().Be(ESubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task Webhook_ACancellationAtTheProvider_EndsTheSubscriptionAndFreesThePlan()
    {
        var advertiser = NewAdvertiser();
        advertiser.ActivePlan = "Profissional";
        var plan = NewPlan();
        var subscription = NewSubscription(advertiser, plan);
        subscription.Status = ESubscriptionStatus.Active;
        subscription.PaymentId = "mp-preapproval:PRE-1";
        await SeedAsync(advertiser, plan, subscription);
        _handler.Enqueue(_ => Task.FromResult(Json(HttpStatusCode.OK,
            $"{{\"id\":\"PRE-1\",\"external_reference\":\"{subscription.Id.Value}\",\"status\":\"cancelled\"}}")));

        var handled = await _service.HandleWebhookAsync(
            """{"type":"subscription_preapproval","data":{"id":"PRE-1"}}""",
            Sign("PRE-1"), "req-1", "PRE-1");

        handled.Should().BeTrue();
        await using var context = _factory.CreateDbContext();
        var stored = await context.Subscriptions.Include(s => s.Advertiser).SingleAsync();
        stored.Status.Should().Be(ESubscriptionStatus.Cancelled);
        stored.Advertiser!.ActivePlan.Should().Be("Free");
    }

    [Fact]
    public async Task Cancel_WithoutAProviderReference_IsANoOp()
    {
        (await _service.CancelAsync(null)).Should().BeTrue();
        (await _service.CancelAsync("outro-gateway:123")).Should().BeTrue();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_SendsACancelledStatusToTheProvider()
    {
        _handler.Enqueue(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        (await _service.CancelAsync("mp-preapproval:PRE-1")).Should().BeTrue();

        var sent = _handler.Requests.Should().ContainSingle().Subject;
        sent.Method.Should().Be(HttpMethod.Put);
        sent.RequestUri!.AbsolutePath.Should().Be("/preapproval/PRE-1");
    }

    [Fact]
    public async Task Cancel_WhenTheProviderRefuses_ReturnsFalse()
    {
        _handler.Enqueue(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        (await _service.CancelAsync("mp-preapproval:PRE-1")).Should().BeFalse();
    }

    private static string Sign(string dataId, string requestId = "req-1", string secret = Secret)
    {
        const string ts = "1762966800";
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest)));
        return $"ts={ts},v1={hash}";
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private async Task SeedAsync(params BaseEntity[] entities)
    {
        await using var context = _factory.CreateDbContext();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    private static Advertiser NewAdvertiser() => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = "default",
        Name = "Empresa Teste",
        Email = "financeiro@empresa.com.br",
        ActivePlan = "Free"
    };

    private static SubscriptionPlan NewPlan() => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = "default",
        Name = "Profissional",
        Price = 49.90m,
        Audience = EPlanAudience.Advertiser,
        Period = ESubscriptionPeriod.Monthly,
        IsActive = true
    };

    private static Subscription NewSubscription(Advertiser advertiser, SubscriptionPlan plan) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = "default",
        AdvertiserId = advertiser.Id,
        PlanId = plan.Id,
        StartDate = Now,
        EndDate = Now.AddDays(7),
        Status = ESubscriptionStatus.Pending
    };

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _responses = new();
        public List<HttpRequestMessage> Requests { get; } = [];

        public void Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) => _responses.Enqueue(respond);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return _responses.Count == 0
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway))
                : _responses.Dequeue()(request);
        }
    }
}
