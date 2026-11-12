using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.Web.Services;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "Billing:MercadoPago";
    public string AccessToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public sealed record MercadoPagoCheckoutResponse(string InitPoint, Guid SubscriptionId);

/// <summary>Checkout recorrente e confirmação exclusivamente por webhook assinado do Mercado Pago.</summary>
public sealed class MercadoPagoBillingService : IRecurringPaymentGateway
{
    private const string PaymentPrefix = "mp-preapproval:";
    private readonly HttpClient _http;
    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IOptionsMonitor<MercadoPagoOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<MercadoPagoBillingService> _logger;

    public MercadoPagoBillingService(
        HttpClient http,
        IDbContextFactory<ApplicationDbContext> contexts,
        IOptionsMonitor<MercadoPagoOptions> options,
        TimeProvider time,
        ILogger<MercadoPagoBillingService> logger)
    {
        _http = http;
        _contexts = contexts;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public bool IsConfigured => HasCredentials(_options.CurrentValue)
        && Uri.TryCreate(_options.CurrentValue.PublicBaseUrl, UriKind.Absolute, out var baseUri)
        && baseUri.Scheme == Uri.UriSchemeHttps;

    public async Task<RequestResult<MercadoPagoCheckoutResponse>> CreateCheckoutAsync(
        Guid advertiserId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!HasCredentials(options))
            return RequestResult<MercadoPagoCheckoutResponse>.WithDomainError("A cobrança online ainda não está configurada.");

        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps)
            return RequestResult<MercadoPagoCheckoutResponse>.WithDomainError("A URL pública segura do Monburan não está configurada.");

        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var advertiser = await context.Advertisers.AsNoTracking()
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.Id == Id.Create(advertiserId), cancellationToken);
        if (advertiser is null || string.IsNullOrWhiteSpace(advertiser.Email))
            return RequestResult<MercadoPagoCheckoutResponse>.WithDomainError("Perfil de empresa não encontrado.");

        var plan = await context.SubscriptionPlans.AsNoTracking()
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.IsActive && item.Id == Id.Create(planId), cancellationToken);
        if (plan is null || plan.Audience != EPlanAudience.Advertiser || plan.Price <= 0
            || plan.Period is not (ESubscriptionPeriod.Monthly or ESubscriptionPeriod.Annual))
            return RequestResult<MercadoPagoCheckoutResponse>.WithDomainError("Plano pago inválido ou indisponível.");

        var active = await context.Subscriptions.AsNoTracking().AnyAsync(subscription =>
            !subscription.IsDeleted && subscription.AdvertiserId == Id.Create(advertiserId)
            && subscription.Status == ESubscriptionStatus.Active && subscription.PlanId == plan.Id, cancellationToken);
        if (active)
            return RequestResult<MercadoPagoCheckoutResponse>.WithDomainError("Sua empresa já tem este plano ativo.");

        var now = _time.GetUtcNow().UtcDateTime;
        var pending = new Subscription
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(advertiserId),
            PlanId = plan.Id,
            StartDate = now,
            EndDate = now.AddDays(7),
            Status = ESubscriptionStatus.Pending,
            TenantId = "default"
        };
        context.Subscriptions.Add(pending);
        await context.SaveChangesAsync(cancellationToken);

        var period = plan.Period == ESubscriptionPeriod.Annual ? 12 : 1;
        var backUrl = new Uri(baseUri, "/plans?checkout=returned").ToString();
        var payload = new
        {
            reason = $"Monburan - {plan.Name}",
            external_reference = pending.Id.Value.ToString(),
            payer_email = advertiser.Email,
            back_url = backUrl,
            notification_url = new Uri(baseUri, "/api/v1/webhooks/mercadopago").ToString(),
            auto_recurring = new
            {
                frequency = period,
                frequency_type = "months",
                transaction_amount = plan.Price,
                currency_id = "BRL"
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/preapproval")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key", pending.Id.Value.ToString());
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mercado Pago recusou a criação de checkout: HTTP {StatusCode}", (int)response.StatusCode);
                pending.Status = ESubscriptionStatus.Expired;
                pending.EndDate = _time.GetUtcNow().UtcDateTime;
                pending.CancellationReason = "Falha ao criar checkout no provedor.";
                await context.SaveChangesAsync(cancellationToken);
                return RequestResult<MercadoPagoCheckoutResponse>.WithDomainError("Não foi possível iniciar o pagamento. Tente novamente.");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var providerId = root.TryGetProperty("id", out var idElement) ? idElement.ToString() : null;
            var initPoint = root.TryGetProperty("init_point", out var pointElement) ? pointElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(providerId) || !IsSafeCheckoutUrl(initPoint))
            {
                pending.Status = ESubscriptionStatus.Expired;
                pending.EndDate = _time.GetUtcNow().UtcDateTime;
                pending.CancellationReason = "Resposta de checkout inválida.";
                await context.SaveChangesAsync(cancellationToken);
                return RequestResult<MercadoPagoCheckoutResponse>.WithDomainError("O provedor retornou um checkout inválido.");
            }

            pending.PaymentId = PaymentPrefix + providerId;
            await context.SaveChangesAsync(cancellationToken);
            return RequestResult<MercadoPagoCheckoutResponse>.Success(new(initPoint!, pending.Id.Value));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falha de rede ao criar checkout do Mercado Pago");
            pending.Status = ESubscriptionStatus.Expired;
            pending.EndDate = _time.GetUtcNow().UtcDateTime;
            pending.CancellationReason = "Falha de conexão ao criar checkout.";
            await context.SaveChangesAsync(cancellationToken);
            return RequestResult<MercadoPagoCheckoutResponse>.WithDomainError("Não foi possível conectar ao provedor de pagamento. Tente novamente.");
        }
    }

    public async Task<bool> HandleWebhookAsync(
        string? payload,
        string? signature,
        string? requestId,
        string? dataId,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!HasCredentials(options) || string.IsNullOrWhiteSpace(dataId)
            || !VerifySignature(options.WebhookSecret, dataId, signature, requestId))
            return false;

        try
        {
            using var eventDocument = JsonDocument.Parse(payload ?? "{}");
            var eventType = eventDocument.RootElement.TryGetProperty("type", out var type)
                ? type.GetString()
                : null;
            var isPreapprovalEvent = string.Equals(eventType, "subscription_preapproval", StringComparison.OrdinalIgnoreCase);
            var isPaymentEvent = string.Equals(eventType, "subscription_authorized_payment", StringComparison.OrdinalIgnoreCase);
            if (!isPreapprovalEvent && !isPaymentEvent)
                return true;

            string? providerId;
            string? reference;
            string? status;
            decimal paidAmount = -1m;
            string? currency = null;
            if (isPreapprovalEvent)
            {
                using var preapprovalDocument = await GetProviderResourceAsync(
                    $"preapproval/{Uri.EscapeDataString(dataId)}", cancellationToken);
                if (preapprovalDocument is null) return false;
                var preapproval = preapprovalDocument.RootElement;
                providerId = StringProperty(preapproval, "id");
                reference = StringProperty(preapproval, "external_reference");
                status = StringProperty(preapproval, "status")?.ToLowerInvariant();
                // Autorizar a recorrência só confirma o mandato. Acesso pago começa depois da primeira cobrança aprovada.
                if (status is not ("cancelled" or "paused")) return true;
            }
            else
            {
                using var paymentDocument = await GetProviderResourceAsync(
                    $"authorized_payments/{Uri.EscapeDataString(dataId)}", cancellationToken);
                if (paymentDocument is null) return false;
                var payment = paymentDocument.RootElement;
                if (StringProperty(payment, "id") != dataId) return false;
                if (!string.Equals(StringProperty(payment, "status"), "approved", StringComparison.OrdinalIgnoreCase))
                    return true;

                var preapprovalId = StringProperty(payment, "preapproval_id");
                if (string.IsNullOrWhiteSpace(preapprovalId)) return false;
                using var preapprovalDocument = await GetProviderResourceAsync(
                    $"preapproval/{Uri.EscapeDataString(preapprovalId)}", cancellationToken);
                if (preapprovalDocument is null) return false;
                var preapproval = preapprovalDocument.RootElement;
                providerId = StringProperty(preapproval, "id");
                reference = StringProperty(preapproval, "external_reference");
                status = StringProperty(preapproval, "status")?.ToLowerInvariant();
                paidAmount = payment.TryGetProperty("transaction_amount", out var amountElement)
                    ? amountElement.GetDecimal()
                    : -1m;
                currency = StringProperty(payment, "currency_id");
                if (status != "authorized") return true;
            }

            if (providerId != dataId || !Guid.TryParse(reference, out var subscriptionId))
            {
                // Pagamentos recorrentes usam o ID do pagamento no webhook; a referência é a assinatura local.
                if (!isPaymentEvent || !Guid.TryParse(reference, out subscriptionId))
                    return false;
            }

            if (isPaymentEvent && string.IsNullOrWhiteSpace(providerId))
                return false;

            await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
            var subscription = await context.Subscriptions
                .Include(item => item.Plan)
                .Include(item => item.Advertiser)
                .FirstOrDefaultAsync(item => item.Id == Id.Create(subscriptionId) && !item.IsDeleted, cancellationToken);
            if (subscription is null || (subscription.PaymentId is not null && subscription.PaymentId != PaymentPrefix + providerId)
                || subscription.Advertiser is null || subscription.Plan is null)
                return true;
            subscription.PaymentId ??= PaymentPrefix + providerId;

            if (isPaymentEvent)
            {
                if (paidAmount != subscription.Plan.Price || currency != "BRL" || subscription.Plan.Audience != EPlanAudience.Advertiser)
                {
                    _logger.LogWarning("Mercado Pago webhook: plano e valor da assinatura {SubscriptionId} não conferem", subscriptionId);
                    return false;
                }
                if (subscription.Status == ESubscriptionStatus.Active)
                    return true;

                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                var now = _time.GetUtcNow().UtcDateTime;
                if (subscription.Status == ESubscriptionStatus.Cancelled
                    || (subscription.Status == ESubscriptionStatus.Pending && subscription.EndDate <= now)
                    || (subscription.Status == ESubscriptionStatus.Expired && subscription.EndDate <= now))
                {
                    if (!await CancelAsync(subscription.PaymentId, cancellationToken))
                        return false;
                    subscription.Status = ESubscriptionStatus.Cancelled;
                    subscription.EndDate = now;
                    subscription.CancellationReason = "Checkout expirado antes da confirmação do pagamento.";
                    await context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return true;
                }
                var previous = await context.Subscriptions
                    .Where(item => !item.IsDeleted && item.AdvertiserId == subscription.AdvertiserId
                        && item.Status == ESubscriptionStatus.Active && item.Id != subscription.Id)
                    .ToListAsync(cancellationToken);
                if (previous.Any(old => old.StartDate > subscription.StartDate))
                {
                    if (!await CancelAsync(subscription.PaymentId, cancellationToken))
                        return false;
                    subscription.Status = ESubscriptionStatus.Cancelled;
                    subscription.EndDate = now;
                    subscription.CancellationReason = "Checkout antigo confirmado depois de uma assinatura mais recente.";
                    await context.SaveChangesAsync(cancellationToken);
                    return true;
                }
                foreach (var old in previous)
                {
                    if (!await CancelAsync(old.PaymentId, cancellationToken))
                        return false;
                    old.Status = ESubscriptionStatus.Cancelled;
                    old.EndDate = now;
                    old.CancellationReason = "Substituída após confirmação de pagamento.";
                }
                subscription.Status = ESubscriptionStatus.Active;
                subscription.StartDate = now;
                subscription.EndDate = null;
                subscription.Advertiser.ActivePlan = subscription.Plan.Name;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else if (status is "cancelled" or "paused")
            {
                if (subscription.Status is ESubscriptionStatus.Active or ESubscriptionStatus.Pending)
                {
                    var now = _time.GetUtcNow().UtcDateTime;
                    subscription.Status = status == "cancelled" ? ESubscriptionStatus.Cancelled : ESubscriptionStatus.Expired;
                    subscription.EndDate = now;
                    subscription.CancellationReason = status == "cancelled" ? "Cancelada no Mercado Pago." : "Cobrança recorrente pausada no Mercado Pago.";
                    var anotherActive = await context.Subscriptions.AnyAsync(item => !item.IsDeleted
                        && item.AdvertiserId == subscription.AdvertiserId && item.Status == ESubscriptionStatus.Active
                        && item.Id != subscription.Id, cancellationToken);
                    if (!anotherActive && subscription.Advertiser.ActivePlan == subscription.Plan.Name)
                        subscription.Advertiser.ActivePlan = "Free";
                    await context.SaveChangesAsync(cancellationToken);
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or InvalidOperationException or FormatException)
        {
            _logger.LogWarning(ex, "Mercado Pago webhook: não foi possível processar a notificação");
            return false;
        }
    }

    private async Task<JsonDocument?> GetProviderResourceAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.CurrentValue.AccessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Mercado Pago: consulta da cobrança retornou HTTP {StatusCode}", (int)response.StatusCode);
            return null;
        }

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<bool> CancelAsync(string? paymentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paymentId) || !paymentId.StartsWith(PaymentPrefix, StringComparison.Ordinal))
            return true;

        var options = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.AccessToken))
            return false;

        var providerId = paymentId[PaymentPrefix.Length..];
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"https://api.mercadopago.com/preapproval/{Uri.EscapeDataString(providerId)}")
        {
            Content = JsonContent.Create(new { status = "cancelled" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return true;
            _logger.LogWarning("Mercado Pago recusou o cancelamento recorrente: HTTP {StatusCode}", (int)response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falha de rede ao cancelar assinatura no Mercado Pago");
            return false;
        }
    }

    private static bool HasCredentials(MercadoPagoOptions options) =>
        !string.IsNullOrWhiteSpace(options.AccessToken) && !string.IsNullOrWhiteSpace(options.WebhookSecret);

    private static bool IsSafeCheckoutUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals("mercadopago.com.br", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".mercadopago.com.br", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("mpago.la", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".mpago.la", StringComparison.OrdinalIgnoreCase));

    private static string? StringProperty(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    private static bool VerifySignature(string secret, string dataId, string? signature, string? requestId)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(requestId)) return false;
        var parts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in signature.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length == 2) parts[pair[0]] = pair[1];
        }
        if (!parts.TryGetValue("ts", out var timestamp) || !parts.TryGetValue("v1", out var supplied)) return false;
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{timestamp};";
        var expected = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest)));
        try
        {
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied.ToLowerInvariant()));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
