using System.Net;
using System.Text.Json;

using FluentAssertions;

namespace Monburan.ApiTests;

/// <summary>
/// O contrato de erro da API não pode depender do ambiente. Uma requisição inválida precisa
/// responder 400 com ProblemDetails pequeno, sem stack trace e sem a extensão "exception" —
/// tanto em Development quanto em Production.
/// </summary>
public class ApiErrorContractApiTests : IClassFixture<MonburanProductionApiFactory>, IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _production;
    private readonly HttpClient _development;

    public ApiErrorContractApiTests(MonburanProductionApiFactory production, MonburanApiFactory development)
    {
        _production = production.CreateClient();
        _development = development.CreateClient();
    }

    [Fact]
    public async Task InvalidRequest_InProduction_Returns400WithoutInternalDetails() =>
        await AssertErrorContract(_production);

    [Fact]
    public async Task InvalidRequest_InDevelopment_ReturnsTheSameContractAsProduction() =>
        await AssertErrorContract(_development);

    private static async Task AssertErrorContract(HttpClient client)
    {
        // "size" não é um inteiro: a requisição não faz binding e o framework devolve 400
        var response = await client.GetAsync("/api/v1/jobs?size=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadAsStringAsync();

        // Sem detalhe interno: nem a extensão "exception", nem o tipo da exceção, nem a pilha
        body.Should().NotContain("exception");
        body.Should().NotContain("BadHttpRequestException");
        body.Should().NotContain("   at ");

        // Corpo pequeno: o stack trace que vazava fazia a resposta passar de ~170 bytes para ~2 KB
        body.Length.Should().BeLessThan(400);

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("title").GetString().Should().Be("Requisição inválida");

        // O app mobile usa o traceId para correlacionar a falha com o log do servidor
        root.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
