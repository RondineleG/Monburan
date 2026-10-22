using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Ai;

/// <summary>Escolhe o provedor padrão dos recursos de IA (precisa estar ativo).</summary>
public class SetDefaultAiProviderUseCase : IUseCase<AiProviderActionRequest, AiProviderResponse>
{
    private readonly IAiProviderRepository _repository;
    private readonly IAiProviderCatalog _catalog;
    private readonly IAiSecretReader _secrets;

    public SetDefaultAiProviderUseCase(IAiProviderRepository repository, IAiProviderCatalog catalog, IAiSecretReader secrets)
    {
        _repository = repository;
        _catalog = catalog;
        _secrets = secrets;
    }

    public async Task<RequestResult<AiProviderResponse>> ExecuteAsync(AiProviderActionRequest request, CancellationToken cancellationToken = default)
    {
        var found = await _repository.GetByIdAsync(Id.Create(request.Id), cancellationToken);
        if (found.Status != EResultStatus.Success || found.Data is null)
            return RequestResult<AiProviderResponse>.EntityNotFound("AiProvider", request.Id, "Provedor não encontrado.");

        if (!found.Data.IsEnabled)
            return RequestResult<AiProviderResponse>.WithDomainError("Ative o provedor antes de torná-lo o padrão.");

        var saved = await _repository.SetDefaultAsync(found.Data.Id, request.UpdatedBy, cancellationToken);
        if (saved.Status != EResultStatus.Success || saved.Data is null)
            return RequestResult<AiProviderResponse>.FromResult(saved);

        await _catalog.ReloadAsync();
        return RequestResult<AiProviderResponse>.Success(
            AiProviderMapper.ToResponse(saved.Data, _secrets.IsSet(saved.Data.ApiKeyEnvVar)));
    }
}
