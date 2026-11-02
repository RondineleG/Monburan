using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Protocolo de mensagens da Anthropic (<c>POST {url}/v1/messages</c>, chave em <c>x-api-key</c> e versão no cabeçalho
/// <c>anthropic-version</c>): Claude e serviços que imitam essa API. O texto vem em blocos <c>text</c>; blocos de
/// raciocínio (<c>thinking</c>) são descartados.
/// </summary>
public sealed class AnthropicProtocol : IAiProtocol
{
    public const string ApiVersion = "2023-06-01";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILogger<AnthropicProtocol> _logger;

    public AnthropicProtocol(HttpClient http, ILogger<AnthropicProtocol> logger)
    {
        _http = http;
        _logger = logger;
    }

    public EAiProtocol Protocol => EAiProtocol.Anthropic;

    /// <summary>
    /// <c>{url}/v1/messages</c>; se o endereço já termina em <c>/v1</c>, só <c>/messages</c>. Assim valem tanto
    /// <c>https://api.anthropic.com</c> quanto <c>https://api.anthropic.com/v1</c>.
    /// </summary>
    public static Uri Endpoint(string baseUrl)
    {
        var root = baseUrl.Trim().TrimEnd('/');
        return new Uri(root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? root + "/messages" : root + "/v1/messages");
    }

    public async Task<AiCompletion> SendAsync(
        AiProviderSettings provider, string apiKey, AiCompletionRequest request, CancellationToken cancellationToken)
    {
        var endpoint = Endpoint(provider.BaseUrl);

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(BuildPayload(provider, request), options: JsonOptions)
        };
        message.Headers.Add("x-api-key", apiKey);
        message.Headers.Add("anthropic-version", ApiVersion);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var (body, elapsed) = await AiHttp.SendAsync(_http, message, provider, _logger, cancellationToken).ConfigureAwait(false);

        var completion = Parse(body);
        AiHttp.LogSuccess(_logger, provider, endpoint.Host, elapsed, completion);
        return completion;
    }

    public static Dictionary<string, object?> BuildPayload(AiProviderSettings provider, AiCompletionRequest request)
    {
        AiMessageContent.EnsureVisionIfNeeded(provider, request);
        // O protocolo não tem papel "system" nas mensagens: as instruções vão no campo "system"
        var system = string.Join("\n\n", request.Messages
            .Where(message => message.Role == "system" && !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => message.Content));

        var payload = new Dictionary<string, object?>
        {
            ["model"] = provider.Model.Trim(),
            ["max_tokens"] = Math.Clamp(provider.MaxOutputTokens, 256, 32000),
            ["messages"] = request.Messages
                .Where(message => message.Role != "system")
                .Select(AiMessageContent.Anthropic)
                .ToArray()
        };

        if (system.Length > 0)
            payload["system"] = system;

        if (provider.SendTemperature)
            payload["temperature"] = Math.Clamp(provider.Temperature, 0, 1);

        return payload;
    }

    public static AiCompletion Parse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (!root.TryGetProperty("content", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
                throw new AiClientException(AiFailureKind.InvalidResponse, "A resposta do provedor não traz 'content'.");

            var text = new StringBuilder();
            foreach (var block in blocks.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object
                    && block.TryGetProperty("type", out var type) && type.GetString() == "text"
                    && block.TryGetProperty("text", out var piece) && piece.ValueKind == JsonValueKind.String)
                    text.Append(piece.GetString());
            }

            var content = text.ToString().Trim();
            if (content.Length == 0)
            {
                var stop = root.TryGetProperty("stop_reason", out var reason) && reason.ValueKind == JsonValueKind.String
                    ? reason.GetString()
                    : null;
                throw stop == "max_tokens"
                    ? new AiClientException(AiFailureKind.Truncated, "A resposta foi cortada pelo limite de tokens antes de sair o texto.")
                    : new AiClientException(AiFailureKind.InvalidResponse, "A resposta do provedor veio sem texto.");
            }

            var model = root.TryGetProperty("model", out var modelName) && modelName.ValueKind == JsonValueKind.String
                ? modelName.GetString()
                : null;
            int? inputTokens = null;
            int? outputTokens = null;
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                inputTokens = Int(usage, "input_tokens");
                outputTokens = Int(usage, "output_tokens");
            }

            return new AiCompletion(content, model, inputTokens, outputTokens);
        }
        catch (JsonException exception)
        {
            throw new AiClientException(AiFailureKind.InvalidResponse, "A resposta do provedor não é um JSON válido.", exception);
        }
    }

    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
}
