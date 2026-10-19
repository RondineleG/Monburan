using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>Fala com um provedor de IA em um protocolo (chat completions da OpenAI ou mensagens da Anthropic).</summary>
public interface IAiProtocol
{
    EAiProtocol Protocol { get; }

    /// <exception cref="AiClientException">Falha prevista (rede, tempo, status, resposta inválida).</exception>
    Task<AiCompletion> SendAsync(
        AiProviderSettings provider, string apiKey, AiCompletionRequest request, CancellationToken cancellationToken);
}
