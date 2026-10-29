using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Protocolo de "chat completions" da OpenAI (<c>POST {url}/chat/completions</c>, chave em <c>Authorization: Bearer</c>):
/// OpenAI, OpenRouter, DeepSeek, Xiaomi, Ollama e os demais compatíveis.
/// </summary>
public sealed partial class OpenAiProtocol : IAiProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILogger<OpenAiProtocol> _logger;

    public OpenAiProtocol(HttpClient http, ILogger<OpenAiProtocol> logger)
    {
        _http = http;
        _logger = logger;
    }

    public EAiProtocol Protocol => EAiProtocol.OpenAiCompatible;

    /// <summary><c>{url}/chat/completions</c>, sem barra duplicada.</summary>
    public static Uri Endpoint(string baseUrl) => new(baseUrl.Trim().TrimEnd('/') + "/chat/completions");

    public async Task<AiCompletion> SendAsync(
        AiProviderSettings provider, string apiKey, AiCompletionRequest request, CancellationToken cancellationToken)
    {
        var endpoint = Endpoint(provider.BaseUrl);

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(BuildPayload(provider, request), options: JsonOptions)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var (body, elapsed) = await AiHttp.SendAsync(_http, message, provider, _logger, cancellationToken).ConfigureAwait(false);

        var completion = Parse(body);
        AiHttp.LogSuccess(_logger, provider, endpoint.Host, elapsed, completion);
        return completion;
    }

    public static Dictionary<string, object?> BuildPayload(AiProviderSettings provider, AiCompletionRequest request)
    {
        AiMessageContent.EnsureVisionIfNeeded(provider, request);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = provider.Model.Trim(),
            ["messages"] = request.Messages.Select(AiMessageContent.OpenAi).ToArray()
        };

        if (provider.SendTemperature)
            payload["temperature"] = Math.Clamp(provider.Temperature, 0, 2);

        // Modelos novos da OpenAI recusam max_tokens e pedem max_completion_tokens
        payload[provider.UseMaxCompletionTokens ? "max_completion_tokens" : "max_tokens"] = Math.Clamp(provider.MaxOutputTokens, 256, 32000);

        if (request.JsonObject && provider.UseJsonResponseFormat)
            payload["response_format"] = new { type = "json_object" };

        return payload;
    }

    public static AiCompletion Parse(string body)
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
                var finish = choice.TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String
                    ? reason.GetString()
                    : null;
                throw finish == "length"
                    ? new AiClientException(AiFailureKind.Truncated, "A resposta foi cortada pelo limite de tokens antes de sair o texto.")
                    : new AiClientException(AiFailureKind.InvalidResponse, "A resposta do provedor veio sem texto.");
            }

            var model = root.TryGetProperty("model", out var modelName) && modelName.ValueKind == JsonValueKind.String
                ? modelName.GetString()
                : null;
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

    // Alguns servidores deixam o raciocínio dentro do texto, entre <think> e </think>
    [GeneratedRegex("<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Reasoning();
}
