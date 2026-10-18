using System.Net;
using System.Text;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>
/// Os protocolos de IA da aplicação (com o <c>HttpClient</c> "ai" montado pelo <c>Program</c>: sem redirecionamento,
/// teto de resposta e guarda de rede) falando HTTP de verdade com um servidor local que imita OpenAI e Anthropic.
/// Os testes unitários usam um handler falso; aqui passa pela pilha toda.
/// </summary>
public sealed class AiProtocolWireTests : IClassFixture<MonburanApiFactory>, IAsyncLifetime
{
    private const string Key = "chave-de-teste-wire";

    private readonly MonburanApiFactory _factory;
    private FakeAiProviderServer _server = null!;

    public AiProtocolWireTests(MonburanApiFactory factory) => _factory = factory;

    public async Task InitializeAsync() => _server = await FakeAiProviderServer.StartAsync();

    public async Task DisposeAsync() => await _server.DisposeAsync();

    private IAiProtocol Protocol(EAiProtocol protocol) =>
        _factory.Services.GetServices<IAiProtocol>().Single(item => item.Protocol == protocol);

    private AiProviderSettings Provider(EAiProtocol protocol, int timeoutSeconds = 90, string? baseUrl = null) => new(
        Guid.NewGuid(),
        "Servidor local",
        protocol,
        baseUrl ?? (protocol == EAiProtocol.OpenAiCompatible ? _server.BaseUrl + "/v1" : _server.BaseUrl),
        "modelo-wire",
        "AI_KEY_WIRE",
        IsEnabled: true,
        IsDefault: false,
        TimeoutSeconds: timeoutSeconds,
        MaxOutputTokens: 4000,
        Temperature: 0.4,
        SendTemperature: true,
        UseJsonResponseFormat: true,
        UseMaxCompletionTokens: false);

    private static AiCompletionRequest Request() => new(
        [new AiMessage("system", "Regras."), new AiMessage("user", "Currículo: Pleno — Alfa. Ação: não use acento errado ✓")],
        JsonObject: true);

    [Fact]
    public async Task OpenAi_TalksTheChatCompletionsProtocolOverRealHttp()
    {
        _server.Handler = context => context.Response.WriteAsJsonAsync(new
        {
            id = "x",
            model = "modelo-wire",
            choices = new[] { new { index = 0, finish_reason = "stop", message = new { role = "assistant", content = "{\"summary\":\"Resumo com acentuação: ação ✓\"}" } } },
            usage = new { prompt_tokens = 10, completion_tokens = 20 }
        });

        var completion = await Protocol(EAiProtocol.OpenAiCompatible).SendAsync(Provider(EAiProtocol.OpenAiCompatible), Key, Request(), default);

        _server.RequestPath.Should().Be("/v1/chat/completions");
        _server.Authorization.Should().Be($"Bearer {Key}");
        _server.ApiKeyHeader.Should().BeNullOrEmpty();
        _server.ContentType.Should().StartWith("application/json");
        _server.Body.GetProperty("model").GetString().Should().Be("modelo-wire");
        _server.Body.GetProperty("response_format").GetProperty("type").GetString().Should().Be("json_object");
        _server.Body.GetProperty("messages")[1].GetProperty("content").GetString().Should().Contain("Pleno — Alfa").And.Contain("✓");

        completion.Content.Should().Be("{\"summary\":\"Resumo com acentuação: ação ✓\"}");
        completion.Model.Should().Be("modelo-wire");
        (completion.PromptTokens, completion.CompletionTokens).Should().Be((10, 20));
    }

    [Fact]
    public async Task Anthropic_TalksTheMessagesProtocolOverRealHttp()
    {
        _server.Handler = context => context.Response.WriteAsJsonAsync(new
        {
            id = "msg_1",
            type = "message",
            model = "modelo-wire",
            content = new object[]
            {
                new { type = "thinking", thinking = "raciocínio que não pode vazar" },
                new { type = "text", text = "{\"summary\":\"Resumo com acentuação: ação ✓\"}" }
            },
            stop_reason = "end_turn",
            usage = new { input_tokens = 11, output_tokens = 22 }
        });

        var completion = await Protocol(EAiProtocol.Anthropic).SendAsync(Provider(EAiProtocol.Anthropic), Key, Request(), default);

        _server.RequestPath.Should().Be("/v1/messages");
        _server.ApiKeyHeader.Should().Be(Key);
        _server.AnthropicVersion.Should().Be("2023-06-01");
        _server.Authorization.Should().BeNullOrEmpty();
        _server.ContentType.Should().StartWith("application/json");
        _server.Body.GetProperty("system").GetString().Should().Be("Regras.");
        _server.Body.GetProperty("messages").GetArrayLength().Should().Be(1);
        _server.Body.GetProperty("messages")[0].GetProperty("content").GetString().Should().Contain("Pleno — Alfa").And.Contain("✓");

        completion.Content.Should().Be("{\"summary\":\"Resumo com acentuação: ação ✓\"}");
        completion.Content.Should().NotContain("raciocínio");
        (completion.PromptTokens, completion.CompletionTokens).Should().Be((11, 22));
    }

    public static TheoryData<EAiProtocol, int, AiFailureKind> ErrorStatuses()
    {
        var data = new TheoryData<EAiProtocol, int, AiFailureKind>();
        foreach (var protocol in new[] { EAiProtocol.OpenAiCompatible, EAiProtocol.Anthropic })
        {
            data.Add(protocol, 401, AiFailureKind.Unauthorized);
            data.Add(protocol, 429, AiFailureKind.RateLimited);
            data.Add(protocol, 500, AiFailureKind.ProviderError);
            data.Add(protocol, 504, AiFailureKind.Timeout);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ErrorStatuses))]
    public async Task MapsErrorStatusesOverRealHttp(EAiProtocol protocol, int status, AiFailureKind expected)
    {
        _server.Handler = async context =>
        {
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { error = new { message = "detalhe do provedor" } });
        };

        var act = () => Protocol(protocol).SendAsync(Provider(protocol), Key, Request(), default);

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(expected);
    }

    [Fact]
    public async Task TimesOutWhenTheServerIsTooSlow()
    {
        _server.Handler = async context =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
            await context.Response.WriteAsJsonAsync(new { });
        };

        // O mínimo do tempo limite é 5 s, então este teste leva esse tempo
        var act = () => Protocol(EAiProtocol.OpenAiCompatible)
            .SendAsync(Provider(EAiProtocol.OpenAiCompatible, timeoutSeconds: 1), Key, Request(), default);

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.Timeout);
    }

    [Theory]
    [InlineData(EAiProtocol.OpenAiCompatible)]
    [InlineData(EAiProtocol.Anthropic)]
    public async Task ReportsAnUnreachableServerAsAProviderError(EAiProtocol protocol)
    {
        var act = () => Protocol(protocol).SendAsync(Provider(protocol, baseUrl: "http://127.0.0.1:1/v1"), Key, Request(), default);

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.ProviderError);
    }

    [Fact]
    public async Task NeverFollowsRedirects_SoAProviderCannotBounceTheKeyToAnotherAddress()
    {
        _server.Handler = context =>
        {
            context.Response.StatusCode = StatusCodes.Status302Found;
            context.Response.Headers.Location = "/v1/internal";
            return Task.CompletedTask;
        };

        var act = () => Protocol(EAiProtocol.OpenAiCompatible).SendAsync(Provider(EAiProtocol.OpenAiCompatible), Key, Request(), default);

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.ProviderError);
        _server.InternalHits.Should().Be(0);
    }

    [Fact]
    public async Task RefusesAnAbsurdlyLargeResponse_InsteadOfFillingTheServerMemory()
    {
        _server.Handler = async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(new string('x', 3 * 1024 * 1024), Encoding.UTF8);
        };

        var act = () => Protocol(EAiProtocol.OpenAiCompatible).SendAsync(Provider(EAiProtocol.OpenAiCompatible), Key, Request(), default);

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.ProviderError);
    }
}

/// <summary>Como em produção: <c>Ai:AllowPrivateNetworks</c> falso (só o desenvolvimento libera a rede interna).</summary>
public class PrivateNetworksBlockedApiFactory : MonburanApiFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:AllowPrivateNetworks"] = "false" }));
    }
}

/// <summary>Defesa contra SSRF na pilha real: com a rede interna bloqueada, nada chega ao servidor de destino.</summary>
public sealed class AiPrivateNetworkGuardWireTests : IClassFixture<PrivateNetworksBlockedApiFactory>, IAsyncLifetime
{
    private const string Key = "chave-de-teste-wire";

    private readonly PrivateNetworksBlockedApiFactory _factory;
    private FakeAiProviderServer _server = null!;

    public AiPrivateNetworkGuardWireTests(PrivateNetworksBlockedApiFactory factory) => _factory = factory;

    public async Task InitializeAsync() => _server = await FakeAiProviderServer.StartAsync();

    public async Task DisposeAsync() => await _server.DisposeAsync();

    private Task<AiCompletion> SendAsync(EAiProtocol protocol, string baseUrl)
    {
        var provider = new AiProviderSettings(
            Guid.NewGuid(), "Destino interno", protocol, baseUrl, "modelo", "AI_KEY_WIRE", true, false, 30, 4000, 0.4, true, true, false);

        return _factory.Services.GetServices<IAiProtocol>().Single(item => item.Protocol == protocol)
            .SendAsync(provider, Key, new AiCompletionRequest([new AiMessage("user", "Oi")]), default);
    }

    [Theory]
    [InlineData(EAiProtocol.OpenAiCompatible)]
    [InlineData(EAiProtocol.Anthropic)]
    public async Task ALoopbackAddress_IsBlocked_AndTheServerNeverSeesTheKey(EAiProtocol protocol)
    {
        var baseUrl = protocol == EAiProtocol.OpenAiCompatible ? _server.BaseUrl + "/v1" : _server.BaseUrl;

        var act = () => SendAsync(protocol, baseUrl);

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.BlockedDestination);
        _server.Hits.Should().Be(0);
    }

    [Fact]
    public async Task AHostNameThatResolvesToLoopback_IsBlockedToo()
    {
        var port = new Uri(_server.BaseUrl).Port;

        var act = () => SendAsync(EAiProtocol.OpenAiCompatible, $"http://localhost:{port}/v1");

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.BlockedDestination);
        _server.Hits.Should().Be(0);
    }

    [Theory]
    [InlineData("http://127.0.0.1:1/v1")]
    [InlineData("http://[::1]:1/v1")]
    public async Task ABlockedAddressFailsAsBlocked_NotAsAnOrdinaryNetworkError(string baseUrl)
    {
        var act = () => SendAsync(EAiProtocol.OpenAiCompatible, baseUrl);

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.BlockedDestination);
    }
}
