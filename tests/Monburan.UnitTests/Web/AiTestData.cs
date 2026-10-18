using System.Net;
using System.Text;

using Microsoft.Extensions.Logging;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.UnitTests.Web;

/// <summary>Provedores de IA prontos para os testes. A chave nunca faz parte da configuração: vem do ambiente.</summary>
internal static class AiTestData
{
    public static AiProviderSettings OpenAi() => new(
        Guid.NewGuid(),
        "Provedor de teste",
        EAiProtocol.OpenAiCompatible,
        "https://api.exemplo.com/v1/",
        "modelo-de-teste",
        "AI_KEY_TESTE",
        IsEnabled: true,
        IsDefault: true,
        TimeoutSeconds: 90,
        MaxOutputTokens: 4000,
        Temperature: 0.4,
        SendTemperature: true,
        UseJsonResponseFormat: true,
        UseMaxCompletionTokens: false);

    public static AiProviderSettings Anthropic() => OpenAi() with
    {
        Protocol = EAiProtocol.Anthropic,
        BaseUrl = "https://api.exemplo.com/"
    };

    public static AiProvider Entity(string name = "Provedor", bool enabled = true, bool isDefault = false, string envVar = "AI_KEY_TESTE") => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = "default",
        Name = name,
        Protocol = EAiProtocol.OpenAiCompatible,
        BaseUrl = "https://api.exemplo.com/v1",
        Model = "modelo-de-teste",
        ApiKeyEnvVar = envVar,
        IsEnabled = enabled,
        IsDefault = isDefault
    };
}

/// <summary>Servidor falso: devolve o que o teste mandar e guarda a última requisição (e o corpo) recebida.</summary>
internal sealed class AiStubHandler : HttpMessageHandler
{
    private HttpStatusCode _status = HttpStatusCode.OK;
    private string _content = "{}";
    private Exception? _failure;

    public HttpRequestMessage? Request { get; private set; }

    public string? Body { get; private set; }

    public void Respond(HttpStatusCode status, string content)
    {
        _status = status;
        _content = content;
        _failure = null;
    }

    public void Throw(Exception failure) => _failure = failure;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Request = request;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        if (_failure is not null)
            throw _failure;

        return new HttpResponseMessage(_status) { Content = new StringContent(_content, Encoding.UTF8, "application/json") };
    }
}

/// <summary>Logger que guarda as mensagens (e as exceções) para o teste conferir o que foi registrado.</summary>
internal sealed class AiListLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Messages.Add(formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
}
