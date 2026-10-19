using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Monburan.ApiTests;

/// <summary>
/// Servidor local, em HTTP de verdade, que imita os dois protocolos de IA (chat completions da OpenAI e mensagens da
/// Anthropic). Guarda o que recebeu e responde o que o teste mandar.
/// </summary>
public sealed class FakeAiProviderServer : IAsyncDisposable
{
    private WebApplication _app = null!;
    private JsonDocument? _body;
    private int _hits;
    private int _internalHits;

    /// <summary>Endereço do servidor, sem barra no fim (ex.: <c>http://127.0.0.1:53210</c>).</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    public string? Authorization { get; private set; }

    public string? ApiKeyHeader { get; private set; }

    public string? AnthropicVersion { get; private set; }

    public string? ContentType { get; private set; }

    public string? RequestPath { get; private set; }

    public JsonElement Body => _body!.RootElement;

    /// <summary>Quantas chamadas chegaram aos caminhos dos protocolos.</summary>
    public int Hits => Volatile.Read(ref _hits);

    /// <summary>Quantas chamadas chegaram a <c>/v1/internal</c>, o alvo de um redirecionamento que nunca deve ser seguido.</summary>
    public int InternalHits => Volatile.Read(ref _internalHits);

    public Func<HttpContext, Task> Handler { get; set; } = _ => Task.CompletedTask;

    public static async Task<FakeAiProviderServer> StartAsync()
    {
        var server = new FakeAiProviderServer();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        server._app = builder.Build();
        server._app.MapPost("/v1/chat/completions", server.HandleAsync);
        server._app.MapPost("/v1/messages", server.HandleAsync);
        server._app.MapPost("/v1/internal", _ =>
        {
            Interlocked.Increment(ref server._internalHits);
            return Task.CompletedTask;
        });

        await server._app.StartAsync();
        server.BaseUrl = server._app.Urls.First().TrimEnd('/');
        return server;
    }

    private async Task HandleAsync(HttpContext context)
    {
        Interlocked.Increment(ref _hits);
        Authorization = context.Request.Headers.Authorization;
        ApiKeyHeader = context.Request.Headers["x-api-key"];
        AnthropicVersion = context.Request.Headers["anthropic-version"];
        ContentType = context.Request.ContentType;
        RequestPath = context.Request.Path;

        _body?.Dispose();
        _body = await JsonDocument.ParseAsync(context.Request.Body);

        await Handler(context);
    }

    public async ValueTask DisposeAsync()
    {
        _body?.Dispose();
        await _app.DisposeAsync();
    }
}
