namespace Monburan.Core.Interfaces.Services;

/// <summary>
/// Geração de texto por IA no protocolo de "chat completions" compatível com OpenAI. A aplicação não conhece o
/// provedor: URL, chave e modelo vêm da configuração (seção <c>Ai</c>), então qualquer serviço compatível serve.
/// </summary>
public interface IAiTextClient
{
    /// <summary>Verdadeiro quando URL, chave e modelo estão configurados. Sem isso nenhum recurso de IA é oferecido.</summary>
    bool IsConfigured { get; }

    /// <summary>Nome do provedor para mostrar à pessoa (ex.: "Xiaomi MiMo"), a quem o conteúdo é enviado.</summary>
    string ProviderName { get; }

    /// <summary>Modelo configurado, ou <c>null</c> quando a IA não está configurada.</summary>
    string? ModelName { get; }

    /// <exception cref="AiClientException">
    /// Falha prevista: sem configuração, tempo esgotado, limite do provedor, recusa ou resposta inválida.
    /// </exception>
    Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default);
}
