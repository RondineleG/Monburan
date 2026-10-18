using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Cliente do protocolo de "chat completions" da OpenAI (<c>POST {BaseUrl}/chat/completions</c>), usado por
/// qualquer provedor compatível. Chave, URL e modelo vêm de <see cref="AiOptions"/>. Nunca registra a chave nem o
/// texto enviado; do provedor, só o status e um trecho curto do erro.
/// </summary>
public sealed partial class OpenAiCompatibleClient : IAiTextClient
{
    private const int LoggedErrorLength = 300;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<AiOptions> _options;
    private readonly ILogger<OpenAiCompatibleClient> _logger;

    public OpenAiCompatibleClient(HttpClient http, IOptionsMonitor<AiOptions> options, ILogger<OpenAiCompatibleClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured => _options.CurrentValue.IsConfigured;

    public string ProviderName => _options.CurrentValue.DisplayName;

    public string? ModelName => IsConfigured ? _options.CurrentValue.Model.Trim() : null;

    public async Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.IsConfigured || options.ChatCompletionsUri is not { } endpoint)
            throw new AiClientException(AiFailureKind.NotConfigured, "IA não configurada: defina Ai:BaseUrl, Ai:ApiKey e Ai:Model.");

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(BuildPayload(options, request), options: JsonOptions)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var seconds = Math.Clamp(options.TimeoutSeconds, 5, 300);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));

        var stopwatch = Stopwatch.StartNew();
        HttpStatusCode status;
        string body;
        try
        {
            using var response = await _http.SendAsync(message, timeout.Token);
            status = response.StatusCode;
            body = await response.Content.ReadAsStringAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("IA: {Host} não respondeu em {Seconds}s", endpoint.Host, seconds);
            throw new AiClientException(AiFailureKind.Timeout, $"O provedor de IA não respondeu em {seconds}s.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "IA: falha de rede ao chamar {Host}", endpoint.Host);
            throw new AiClientException(AiFailureKind.ProviderError, "Falha de rede ao chamar o provedor de IA.", exception);
        }

        if ((int)status is < 200 or > 299)
        {
            _logger.LogWarning(
                "IA: {Host} respondeu {Status} em {Elapsed} ms: {Detail}",
                endpoint.Host, (int)status, stopwatch.ElapsedMilliseconds, Excerpt(body));

            throw new AiClientException(KindFor(status), $"O provedor de IA respondeu {(int)status}.");
        }

        var completion = Parse(body);
        _logger.LogInformation(
            "IA: resposta de {Host} em {Elapsed} ms (modelo {Model}, prompt {PromptTokens} tokens, resposta {CompletionTokens} tokens)",
            endpoint.Host, stopwatch.ElapsedMilliseconds, completion.Model ?? options.Model, completion.PromptTokens, completion.CompletionTokens);
        return completion;
    }

    private static Dictionary<string, object?> BuildPayload(AiOptions options, AiCompletionRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = options.Model.Trim(),
            ["messages"] = request.Messages.Select(message => new { role = message.Role, content = message.Content }).ToArray(),
            ["temperature"] = Math.Clamp(options.Temperature, 0, 2),
            ["max_tokens"] = Math.Clamp(options.MaxOutputTokens, 256, 16000)
        };

        if (request.JsonObject && options.UseJsonResponseFormat)
            payload["response_format"] = new { type = "json_object" };

        return payload;
    }

    private static AiFailureKind KindFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiFailureKind.Unauthorized,
        HttpStatusCode.TooManyRequests => AiFailureKind.RateLimited,
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => AiFailureKind.Timeout,
        _ => AiFailureKind.ProviderError
    };

    private static AiCompletion Parse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
                throw new AiClientException(AiFailureKind.InvalidResponse, "A resposta do provedor não traz 'choices'.");

            var choice = choices[0];
            var content = choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var value)
                ? ContentText(value)
                : string.Empty;
            content = Reasoning().Replace(content, string.Empty).Trim();

            if (content.Length == 0)
            {
                // Em modelos de raciocínio o limite de tokens pode acabar antes de sair a resposta
                var finish = choice.TryGetProperty("finish_reason", out var reason) ? reason.GetString() : null;
                throw finish == "length"
                    ? new AiClientException(AiFailureKind.Truncated, "A resposta foi cortada pelo limite de tokens antes de sair o texto.")
                    : new AiClientException(AiFailureKind.InvalidResponse, "A resposta do provedor veio sem texto.");
            }

            var model = root.TryGetProperty("model", out var modelName) ? modelName.GetString() : null;
            int? promptTokens = null;
            int? completionTokens = null;
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                promptTokens = Int(usage, "prompt_tokens");
                completionTokens = Int(usage, "completion_tokens");
            }

            return new AiCompletion(content, model, promptTokens, completionTokens);
        }
        catch (JsonException exception)
        {
            throw new AiClientException(AiFailureKind.InvalidResponse, "A resposta do provedor não é um JSON válido.", exception);
        }
    }

    // "content" é um texto; alguns servidores devolvem uma lista de partes ({"type":"text","text":"..."})
    private static string ContentText(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString() ?? string.Empty;
            case JsonValueKind.Array:
                var text = new StringBuilder();
                foreach (var part in value.EnumerateArray())
                {
                    if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var piece) && piece.ValueKind == JsonValueKind.String)
                        text.Append(piece.GetString());
                }

                return text.ToString();
            default:
                return string.Empty;
        }
    }

    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static string Excerpt(string body)
    {
        var flat = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= LoggedErrorLength ? flat : flat[..LoggedErrorLength] + "…";
    }

    // Alguns servidores deixam o raciocínio dentro do texto, entre <think> e </think>
    [GeneratedRegex("<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Reasoning();
}
