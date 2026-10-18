using System.Net;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>
/// O cliente de IA falando HTTP de verdade (socket, cabeçalhos e JSON reais) com um servidor local que imita o
/// protocolo de chat completions da OpenAI. Os testes unitários usam um handler falso; aqui passa pela pilha toda.
/// </summary>
public sealed class OpenAiCompatibleClientWireTests : IAsyncLifetime
{
    private const string Key = "chave-de-teste-wire";

    private WebApplication _server = null!;
    private string _baseUrl = string.Empty;
    private string? _authorization;
    private string? _contentType;
    private JsonDocument? _body;
    private Func<HttpContext, Task> _handler = _ => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _server = builder.Build();
        _server.MapPost("/v1/chat/completions", async (HttpContext context) =>
        {
            _authorization = context.Request.Headers.Authorization;
            _contentType = context.Request.ContentType;
            _body = await JsonDocument.ParseAsync(context.Request.Body);
            await _handler(context);
        });

        await _server.StartAsync();
        _baseUrl = _server.Urls.First().TrimEnd('/') + "/v1";
    }

    public async Task DisposeAsync()
    {
        _body?.Dispose();
        await _server.DisposeAsync();
    }

    private OpenAiCompatibleClient Client(Action<AiOptions>? configure = null)
    {
        var options = new AiOptions { BaseUrl = _baseUrl, ApiKey = Key, Model = "modelo-wire", ProviderName = "Servidor local" };
        configure?.Invoke(options);
        return new OpenAiCompatibleClient(new HttpClient(), new FixedOptions(options), NullLogger<OpenAiCompatibleClient>.Instance);
    }

    private static AiCompletionRequest Request() =>
        new([new AiMessage("system", "Regras."), new AiMessage("user", "Currículo: Pleno — Alfa. Ação: não use acento errado ✓")], JsonObject: true);

    [Fact]
    public async Task CompleteAsync_TalksTheOpenAiProtocolOverRealHttp()
    {
        _handler = context => context.Response.WriteAsJsonAsync(new
        {
            id = "x",
            model = "modelo-wire",
            choices = new[] { new { index = 0, finish_reason = "stop", message = new { role = "assistant", content = "{\"summary\":\"Resumo com acentuação: ação ✓\"}" } } },
            usage = new { prompt_tokens = 10, completion_tokens = 20 }
        });

        var completion = await Client().CompleteAsync(Request());

        _authorization.Should().Be($"Bearer {Key}");
        _contentType.Should().StartWith("application/json");
        var root = _body!.RootElement;
        root.GetProperty("model").GetString().Should().Be("modelo-wire");
        root.GetProperty("response_format").GetProperty("type").GetString().Should().Be("json_object");
        root.GetProperty("messages")[1].GetProperty("content").GetString().Should().Contain("Pleno — Alfa").And.Contain("✓");

        completion.Content.Should().Be("{\"summary\":\"Resumo com acentuação: ação ✓\"}");
        completion.Model.Should().Be("modelo-wire");
        (completion.PromptTokens, completion.CompletionTokens).Should().Be((10, 20));
    }

    [Theory]
    [InlineData(401, AiFailureKind.Unauthorized)]
    [InlineData(429, AiFailureKind.RateLimited)]
    [InlineData(500, AiFailureKind.ProviderError)]
    [InlineData(504, AiFailureKind.Timeout)]
    public async Task CompleteAsync_MapsErrorStatusesOverRealHttp(int status, AiFailureKind expected)
    {
        _handler = async context =>
        {
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { error = new { message = "detalhe do provedor" } });
        };

        var act = () => Client().CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(expected);
    }

    [Fact]
    public async Task CompleteAsync_TimesOutWhenTheServerIsTooSlow()
    {
        _handler = async context =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
            await context.Response.WriteAsJsonAsync(new { });
        };

        // O mínimo do tempo limite é 5 s, então este teste leva esse tempo
        var act = () => Client(options => options.TimeoutSeconds = 1).CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.Timeout);
    }

    [Fact]
    public async Task CompleteAsync_ReportsAnUnreachableServerAsAProviderError()
    {
        var act = () => Client(options => options.BaseUrl = "http://127.0.0.1:1/v1").CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.ProviderError);
    }

    private sealed class FixedOptions(AiOptions value) : Microsoft.Extensions.Options.IOptionsMonitor<AiOptions>
    {
        public AiOptions CurrentValue => value;

        public AiOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<AiOptions, string?> listener) => null;
    }
}
