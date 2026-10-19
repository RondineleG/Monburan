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
            var result = await useCase.ExecuteAsync(advertiserId);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithTags("Subscriptions")
        .WithSummary("Lista planos disponíveis (e assinatura atual, se anunciante logado)");

        var group = api.MapGroup("/subscriptions").WithTags("Subscriptions")
            .RequireAuthorization("ApiBearer");

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
            var result = await useCase.ExecuteAsync(request);
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

            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult();
        })
        .WithSummary("Cancela assinatura do anunciante autenticado");

        return api;
    }
}
