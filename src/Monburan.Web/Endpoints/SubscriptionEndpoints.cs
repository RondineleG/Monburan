using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class SubscriptionEndpoints
{
    public static RouteGroupBuilder MapSubscriptionEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/webhooks/mercadopago", async (HttpContext http, MercadoPagoBillingService billing, CancellationToken cancellationToken) =>
        {
            if (http.Request.ContentLength > 64 * 1024)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            await using var payloadBuffer = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await http.Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (payloadBuffer.Length + read > 64 * 1024)
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                await payloadBuffer.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            var payload = Encoding.UTF8.GetString(payloadBuffer.ToArray());
            string? dataId = http.Request.Query["data.id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dataId))
            {
                try
                {
                    using var document = JsonDocument.Parse(payload);
                    if (document.RootElement.TryGetProperty("data", out var data)
                        && data.TryGetProperty("id", out var id))
                        dataId = id.ValueKind == JsonValueKind.String ? id.GetString() : id.ToString();
                }
                catch (JsonException)
                {
                    return Results.BadRequest();
                }
            }

            var handled = await billing.HandleWebhookAsync(
                payload,
                http.Request.Headers["x-signature"].FirstOrDefault(),
                http.Request.Headers["x-request-id"].FirstOrDefault(),
                dataId,
                cancellationToken);
            return Results.StatusCode(handled ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        }).AllowAnonymous().WithTags("Billing").WithSummary("Webhook assinado de assinaturas Mercado Pago");

        // Planos: anônimo vê a lista; autenticado também recebe CurrentSubscription.
        // AllowAnonymous + DefaultScheme=cookie faz o bearer nunca ser executado —
        // autentica o JWT explicitamente quando vier no header.
        api.MapGet("/plans", async (
            HttpContext httpContext,
            ICurrentUserHelper currentUser,
            GetAvailablePlansUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (httpContext.User.Identity?.IsAuthenticated != true
                && httpContext.Request.Headers.Authorization.Count > 0)
            {
                var auth = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                // Só aceita access token — refresh token não identifica perfil aqui
                if (auth.Succeeded && auth.Principal is not null
                    && auth.Principal.FindFirst(IdentityService.TokenUseClaim)?.Value == IdentityService.TokenUseAccess)
                {
                    httpContext.User = auth.Principal;
                }
            }

            var advertiserId = currentUser.GetAdvertiserId() ?? Guid.Empty;
            var result = await useCase.ExecuteAsync(advertiserId, cancellationToken);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithTags("Subscriptions")
        .WithSummary("Lista planos disponíveis (e assinatura atual, se anunciante logado)");

        var group = api.MapGroup("/subscriptions").WithTags("Subscriptions")
            .RequireAuthorization("ApiBearer");

        group.MapPost("/checkout", async (
            CreateSubscriptionCheckoutRequest request,
            ICurrentUserHelper currentUser,
            MercadoPagoBillingService billing,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            var result = await billing.CreateCheckoutAsync(advertiserId.Value, request.PlanId, cancellationToken);
            return result.Status == EResultStatus.Success && result.Data is not null
                ? Results.Ok(result.Data)
                : result.ToHttpResult();
        })
        .WithSummary("Cria checkout recorrente seguro para o anunciante autenticado");

        group.MapPost("/upgrade", async (
            UpgradePlanRequest request,
            ICurrentUserHelper currentUser,
            UpgradePlanUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            request.AdvertiserId = advertiserId.Value;
            var result = await useCase.ExecuteAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .WithSummary("Faz upgrade do plano do anunciante autenticado");

        group.MapPost("/cancel", async (
            CancelSubscriptionRequest request,
            ICurrentUserHelper currentUser,
            CancelSubscriptionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            // O anunciante vem da claim, nunca do corpo: a checagem de posse da assinatura é
            // feita pelo use case com este valor, para o cancelamento não poder ser usado
            // contra a assinatura de outro anunciante.
            request.AdvertiserId = advertiserId.Value;

            var result = await useCase.ExecuteAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .WithSummary("Cancela assinatura do anunciante autenticado");

        return api;
    }
}
