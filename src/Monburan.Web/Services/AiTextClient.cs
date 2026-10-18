using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Cliente de IA da aplicação: escolhe o provedor (o padrão, ou um específico no teste do administrador), busca a chave
/// na variável de ambiente dele e fala o protocolo certo. Nada é guardado nem registrado além do que cada protocolo loga.
/// </summary>
public sealed class AiTextClient : IAiTextClient
{
    private readonly IAiProviderCatalog _catalog;
    private readonly IAiSecretReader _secrets;
    private readonly IReadOnlyList<IAiProtocol> _protocols;

    public AiTextClient(IAiProviderCatalog catalog, IAiSecretReader secrets, IEnumerable<IAiProtocol> protocols)
    {
        _catalog = catalog;
        _secrets = secrets;
        _protocols = protocols.ToList();
    }

    public bool IsConfigured => _catalog.Default is { } provider && _secrets.IsSet(provider.ApiKeyEnvVar);

    public string ProviderName => _catalog.Default?.Name ?? "provedor de IA";

    public string? ModelName => IsConfigured ? _catalog.Default!.Model : null;

    public Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var provider = _catalog.Default
            ?? throw new AiClientException(AiFailureKind.NotConfigured, "Nenhum provedor de IA padrão e ativo.");

        return CompleteAsync(provider, request, cancellationToken);
    }

    public Task<AiCompletion> CompleteAsync(Guid providerId, AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var provider = _catalog.All.FirstOrDefault(item => item.Id == providerId)
            ?? throw new AiClientException(AiFailureKind.NotConfigured, "Provedor de IA não encontrado.");

        return CompleteAsync(provider, request, cancellationToken);
    }

    private async Task<AiCompletion> CompleteAsync(AiProviderSettings provider, AiCompletionRequest request, CancellationToken cancellationToken)
    {
        var apiKey = _secrets.Get(provider.ApiKeyEnvVar)
            ?? throw new AiClientException(AiFailureKind.NotConfigured, $"A variável {provider.ApiKeyEnvVar} não está definida neste servidor.");

        var protocol = _protocols.FirstOrDefault(item => item.Protocol == provider.Protocol)
            ?? throw new AiClientException(AiFailureKind.NotConfigured, $"Protocolo {provider.Protocol} sem implementação.");

        return await protocol.SendAsync(provider, apiKey, request, cancellationToken).ConfigureAwait(false);
    }
}
