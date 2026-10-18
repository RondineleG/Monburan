using System.Diagnostics;
using System.Net;

using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Envio e tratamento comum das chamadas aos provedores: tempo limite próprio, destino bloqueado, falhas de rede e
/// status de erro viram <see cref="AiClientException"/>. No log vão o provedor, o servidor, o status e um trecho curto do
/// erro; nunca a chave nem o texto enviado.
/// </summary>
internal static class AiHttp
{
    public const string ClientName = "ai";
    private const int LoggedErrorLength = 300;

    public static async Task<(string Body, long ElapsedMilliseconds)> SendAsync(
        HttpClient http,
        HttpRequestMessage message,
        AiProviderSettings provider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var host = message.RequestUri?.Host ?? "?";
        var seconds = Math.Clamp(provider.TimeoutSeconds, 5, 300);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));

        var stopwatch = Stopwatch.StartNew();
        HttpStatusCode status;
        string body;
        try
        {
            using var response = await http.SendAsync(message, timeout.Token).ConfigureAwait(false);
            status = response.StatusCode;
            body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("IA: {Provider} ({Host}) não respondeu em {Seconds}s", provider.Name, host, seconds);
            throw new AiClientException(AiFailureKind.Timeout, $"O provedor de IA não respondeu em {seconds}s.");
        }
        catch (HttpRequestException exception) when (IsBlocked(exception))
        {
            logger.LogWarning("IA: conexão com {Provider} ({Host}) bloqueada: o endereço resolve para rede interna ou reservada", provider.Name, host);
            throw new AiClientException(AiFailureKind.BlockedDestination, $"Destino bloqueado: {host} aponta para uma rede interna ou reservada.", exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "IA: falha de rede ao chamar {Provider} ({Host})", provider.Name, host);
            throw new AiClientException(AiFailureKind.ProviderError, "Falha de rede ao chamar o provedor de IA.", exception);
        }

        if ((int)status is < 200 or > 299)
        {
            logger.LogWarning(
                "IA: {Provider} ({Host}) respondeu {Status} em {Elapsed} ms: {Detail}",
                provider.Name, host, (int)status, stopwatch.ElapsedMilliseconds, Excerpt(body));

            throw new AiClientException(KindFor(status), $"O provedor de IA respondeu {(int)status}.");
        }

        return (body, stopwatch.ElapsedMilliseconds);
    }

    public static void LogSuccess(ILogger logger, AiProviderSettings provider, string host, long elapsed, AiCompletion completion) =>
        logger.LogInformation(
            "IA: resposta de {Provider} ({Host}) em {Elapsed} ms (modelo {Model}, prompt {PromptTokens} tokens, resposta {CompletionTokens} tokens)",
            provider.Name, host, elapsed, completion.Model ?? provider.Model, completion.PromptTokens, completion.CompletionTokens);

    private static bool IsBlocked(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is AiBlockedDestinationException)
                return true;
        }

        return false;
    }

    private static AiFailureKind KindFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiFailureKind.Unauthorized,
        HttpStatusCode.TooManyRequests => AiFailureKind.RateLimited,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => AiFailureKind.Timeout,
        _ => AiFailureKind.ProviderError
    };

    private static string Excerpt(string body)
    {
        var flat = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= LoggedErrorLength ? flat : flat[..LoggedErrorLength] + "…";
    }
}
