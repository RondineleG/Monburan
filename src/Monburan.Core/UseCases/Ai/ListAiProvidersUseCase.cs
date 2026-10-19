using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Ai;

/// <summary>Provedores de IA cadastrados, com a informação de a variável da chave existir no servidor (nunca o valor).</summary>
public class ListAiProvidersUseCase
{
    private readonly IAiProviderRepository _repository;
    private readonly IAiSecretReader _secrets;

    public ListAiProvidersUseCase(IAiProviderRepository repository, IAiSecretReader secrets)
    {
        _repository = repository;
        _secrets = secrets;
    }

    public async Task<RequestResult<IReadOnlyList<AiProviderResponse>>> ExecuteAsync()
    {
        var result = await _repository.ListAsync();
        if (result.Status != EResultStatus.Success || result.Data is null)
            return RequestResult<IReadOnlyList<AiProviderResponse>>.FromResult(result);

        IReadOnlyList<AiProviderResponse> providers = result.Data
            .Select(provider => AiProviderMapper.ToResponse(provider, _secrets.IsSet(provider.ApiKeyEnvVar)))
            .ToList();
        return RequestResult<IReadOnlyList<AiProviderResponse>>.Success(providers);
    }
}
