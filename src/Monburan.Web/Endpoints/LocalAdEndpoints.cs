using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class LocalAdEndpoints
{
    public static IEndpointRouteBuilder MapLocalAdEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/ads/{id:guid}/click", async (
            Guid id,
            LocalAdCampaignService campaigns,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var target = await campaigns.RegisterClickAndGetTargetAsync(id, cancellationToken);
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return Results.NotFound();

            http.Response.Headers["Referrer-Policy"] = "no-referrer";
            http.Response.Headers.CacheControl = "no-store";
            return Results.Redirect(uri.ToString());
        })
        .AllowAnonymous()
        .ExcludeFromDescription();

        return endpoints;
    }
}
