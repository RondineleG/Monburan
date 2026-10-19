using System.Net;

using FluentAssertions;

using Monburan.Core.DTOs.Response;

using System.Net.Http.Json;

namespace Monburan.ApiTests;

/// <summary>robots.txt e sitemap.xml, as duas rotas anônimas fora da API que dirigem a indexação.</summary>
public class SeoApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public SeoApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Robots_AllowsThePublicSite_AndPointsToTheSitemap()
    {
        var response = await _client.GetAsync("/robots.txt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");

        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("User-agent: *");
        text.Should().Contain("Allow: /");
        text.Should().Contain("Disallow: /api/");
        text.Should().Contain("Disallow: /admin/");
        text.Should().Contain("Disallow: /Account/");
        text.Should().MatchRegex(@"Sitemap: \S+/sitemap\.xml");
    }

    [Fact]
    public async Task Sitemap_ListsHomeJobsAndTheActiveJobs()
    {
        var jobs = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=100");

        var response = await _client.GetAsync("/sitemap.xml");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var xml = await response.Content.ReadAsStringAsync();
        xml.Should().Contain($"<loc>{_client.BaseAddress}</loc>", "a home entra no mapa");
        xml.Should().Contain($"<loc>{_client.BaseAddress}jobs</loc>", "a listagem de vagas entra no mapa");
        foreach (var job in jobs!.Jobs)
            xml.Should().Contain($"/jobs/{job.Id}</loc>", $"a vaga {job.Id} precisa estar no mapa");
    }

    [Fact]
    public async Task Sitemap_HasXmlContentTypeAndPublicCache()
    {
        var response = await _client.GetAsync("/sitemap.xml");

        response.Content.Headers.ContentType!.ToString().Should().Be("application/xml; charset=utf-8");
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromHours(1));
    }
}
