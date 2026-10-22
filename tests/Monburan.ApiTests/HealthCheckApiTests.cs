using System.Net;
using System.Text.Json;

using FluentAssertions;

namespace Monburan.ApiTests;

/// <summary>
/// Contrato dos endpoints de saúde: o <c>/health</c> continua sendo liveness em texto simples
/// (o deploy.sh, o docker-compose e o nginx dependem desse corpo exato), o <c>/health/live</c> é
/// liveness sem depender do banco e o <c>/health/ready</c> só fica "ready" quando o banco
/// responde.
/// </summary>
public class HealthCheckApiTests : IClassFixture<MonburanProductionApiFactory>, IClassFixture<MonburanFailingHealthCheckApiFactory>
{
    private readonly MonburanProductionApiFactory _factory;
    private readonly MonburanFailingHealthCheckApiFactory _factoryWithFailingCheck;

    public HealthCheckApiTests(MonburanProductionApiFactory factory, MonburanFailingHealthCheckApiFactory factoryWithFailingCheck)
    {
        _factory = factory;
        _factoryWithFailingCheck = factoryWithFailingCheck;
    }

    [Fact]
    public async Task Health_KeepsTheLegacyPlainTextContract()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Live_Answers200WithoutTheDatabase()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Ready_ReportsTheDatabaseCheckAsJson()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("status").GetString().Should().Be("Healthy");

        var checks = root.GetProperty("checks").EnumerateArray().ToArray();
        checks.Should().Contain(check => check.GetProperty("name").GetString() == "banco");
        checks.Should().OnlyContain(check => check.GetProperty("status").GetString() == "Healthy");
    }

    [Fact]
    public async Task Ready_Returns503WhenACheckFails()
    {
        var client = _factoryWithFailingCheck.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("status").GetString().Should().Be("Unhealthy");
        root.GetProperty("checks").EnumerateArray().Should().Contain(check =>
            check.GetProperty("name").GetString() == "teste-sempre-falho" &&
            check.GetProperty("status").GetString() == "Unhealthy");
    }

    [Fact]
    public async Task HealthAndLive_IgnoreTheFailingCheck()
    {
        var client = _factoryWithFailingCheck.CreateClient();

        // Liveness não depende do banco: o orquestrador não reinicia o contêiner porque o
        // Postgres caiu, e o deploy não trava o boot esperando o banco.
        foreach (var path in new[] { "/health", "/health/live" })
        {
            var response = await client.GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.OK, path);
            (await response.Content.ReadAsStringAsync()).Should().Be("Healthy", path);
        }
    }
}
