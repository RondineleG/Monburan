namespace Monburan.Core.Interfaces.Services;

/// <summary>
/// Geração de texto por IA. A aplicação não conhece o provedor: o administrador cadastra os provedores (endereço,
/// modelo, protocolo e a variável de ambiente da chave) e escolhe o padrão, e este cliente fala o protocolo de cada um
/// (chat completions da OpenAI ou mensagens da Anthropic).
/// </summary>
public interface IAiTextClient
{
    /// <summary>Há um provedor padrão ativo e a chave dele existe no servidor. Sem isso nenhum recurso de IA é oferecido.</summary>
    bool IsConfigured { get; }

    /// <summary>Nome do provedor padrão para mostrar à pessoa (ex.: "Xiaomi MiMo"), a quem o conteúdo é enviado.</summary>
    string ProviderName { get; }

    /// <summary>Modelo do provedor padrão, ou <c>null</c> quando a IA não está configurada.</summary>
    string? ModelName { get; }

    /// <summary>Usa o provedor padrão.</summary>
    /// <exception cref="AiClientException">
    /// Falha prevista: sem configuração, destino bloqueado, tempo esgotado, limite do provedor, recusa ou resposta inválida.
    /// </exception>
    Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Usa um provedor específico, ativo ou não (teste do administrador antes de ativar).</summary>
    /// <exception cref="AiClientException">As mesmas de <see cref="CompleteAsync(AiCompletionRequest, CancellationToken)"/>.</exception>
    Task<AiCompletion> CompleteAsync(Guid providerId, AiCompletionRequest request, CancellationToken cancellationToken = default);
}
