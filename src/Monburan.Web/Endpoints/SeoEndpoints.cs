using System.Text;

using Monburan.Core.Interfaces.Repositories;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

/// <summary>
/// robots.txt e sitemap.xml, anônimos e fora da API: são eles que dizem aos buscadores o que indexar do site.
/// </summary>
public static class SeoEndpoints
{
    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", (
            IConfiguration configuration,
            HttpContext context) =>
        {
            var baseUri = RequestBaseUri(context);
            var text = new StringBuilder()
                .AppendLine("User-agent: *")
                .AppendLine("Allow: /")
                .AppendLine("Disallow: /api/")
                .AppendLine("Disallow: /admin/")
                .AppendLine("Disallow: /Account/")
                .AppendLine("Disallow: /entrar")
                .AppendLine("Disallow: /criar-conta")
                .AppendLine("Disallow: /esqueci-senha")
                .AppendLine("Disallow: /redefinir-senha")
                .AppendLine("Disallow: /confirmar-email")
                .AppendLine("Disallow: /reenviar-confirmacao")
                .AppendLine("Disallow: /conta")
                .AppendLine()
                .Append("Sitemap: ").Append(PublicUrls.Absolute(configuration, baseUri, "/sitemap.xml"))
                .ToString();

            return Results.Text(text, "text/plain; charset=utf-8");
        })
        .AllowAnonymous()
        .WithSummary("Regras de indexação do site para os buscadores");

        app.MapGet("/sitemap.xml", async (
            IConfiguration configuration,
            HttpContext context,
            IJobRepository jobs,
            CancellationToken cancellationToken) =>
        {
            var baseUri = RequestBaseUri(context);
            var urls = new List<SitemapUrl>
            {
                new(PublicUrls.Absolute(configuration, baseUri, "/"), null),
                new(PublicUrls.Absolute(configuration, baseUri, "/jobs"), null)
            };

            var activeJobs = await jobs.ListActiveForRecommendationsAsync(cancellationToken);
            if (activeJobs.Data is not null)
            {
                urls.AddRange(activeJobs.Data.Select(job => new SitemapUrl(
                    PublicUrls.Absolute(configuration, baseUri, $"/jobs/{job.Id.Value}"),
                    job.UpdatedAt ?? job.CreatedAt)));
            }

            context.Response.Headers.CacheControl = "public, max-age=3600";
            return Results.Text(SitemapBuilder.Build(urls), "application/xml; charset=utf-8");
        })
        .AllowAnonymous()
        .WithSummary("Mapa do site com as vagas ativas e não expiradas");

        return app;
    }

    private static string RequestBaseUri(HttpContext context) =>
        $"{context.Request.Scheme}://{context.Request.Host}";
}
