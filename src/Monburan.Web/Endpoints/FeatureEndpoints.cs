using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class FeatureEndpoints
{
    public static RouteGroupBuilder MapFeatureEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/features").WithTags("Features");

        // Anônimo recebe só as liberadas para todos; com bearer válido, também as dos grupos da conta
        group.MapGet("/", async (HttpContext http, IFeatureFlags flags, CancellationToken cancellationToken) =>
        {
            var user = await BearerUserAsync(http);
            return Results.Ok(new FeatureListResponse { Features = [.. await flags.EnabledFeaturesAsync(user, cancellationToken)] });
        })
        .AllowAnonymous()
        .WithSummary("Funcionalidades ligadas para quem chama (o app esconde o que não estiver na lista)");

        return group;
    }

    /// <summary>
    /// Só responde se a funcionalidade estiver ligada para a conta; caso contrário, 404 (como se o endpoint não existisse).
    /// Use nos endpoints de funcionalidades novas: <c>group.MapPost(...).RequireFeature("nova-busca")</c>.
    /// </summary>
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, string feature) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var flags = http.RequestServices.GetRequiredService<IFeatureFlags>();
            var user = http.User.Identity?.IsAuthenticated == true ? http.User : await BearerUserAsync(http);
            return await flags.IsEnabledAsync(feature, user, http.RequestAborted)
                ? await next(context)
                : Results.NotFound();
        });

    private static async Task<ClaimsPrincipal?> BearerUserAsync(HttpContext http)
    {
        if (http.User.Identity?.IsAuthenticated == true)
            return http.User;

        var result = await http.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        // Refresh token não vale como sessão (mesma regra da política ApiBearer)
        return result.Succeeded && result.Principal.HasClaim(IdentityService.TokenUseClaim, IdentityService.TokenUseAccess)
            ? result.Principal
            : null;
    }
}
