using System.Net;

using FluentAssertions;

namespace Monburan.ApiTests;

/// <summary>
/// Rota que não existe no site precisa chegar ao navegador com uma página de verdade, e não com corpo vazio.
/// A API continua respondendo ProblemDetails, e arquivo estático continua 404 seco (sem renderizar o site).
/// </summary>
public class NotFoundPageApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public NotFoundPageApiTests(MonburanApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task UnknownWebRoute_RendersTheNotFoundPage()
    {
        var response = await _client.GetAsync("/rota-que-nao-existe-" + Guid.NewGuid().ToString("N"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Página não encontrada");
        html.Should().Contain("href=\"/jobs\"");
    }

    [Fact]
    public async Task UnknownApiRoute_KeepsProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/rota-que-nao-existe");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":404");
        body.Should().NotContain("Página não encontrada");
    }

    [Fact]
    public async Task MissingStaticFile_DoesNotRenderTheSite()
    {
        var response = await _client.GetAsync("/arquivo-inexistente-" + Guid.NewGuid().ToString("N") + ".css");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Página não encontrada");
    }
}
