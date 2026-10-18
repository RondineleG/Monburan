using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Ai;

/// <summary>
/// Exclui um provedor. O padrão só pode ser excluído quando for o único (aí a IA fica desligada); havendo outros, é
/// preciso escolher outro padrão antes.
/// </summary>
public class DeleteAiProviderUseCase
{
    private readonly IAiProviderRepository _repository;
    private readonly IAiProviderCatalog _catalog;

    public DeleteAiProviderUseCase(IAiProviderRepository repository, IAiProviderCatalog catalog)
    {
        _repository = repository;
        _catalog = catalog;
    }

    public async Task<RequestResult> ExecuteAsync(AiProviderActionRequest request)
    {
        var found = await _repository.GetByIdAsync(Id.Create(request.Id));
        if (found.Status != EResultStatus.Success || found.Data is null)
            return RequestResult.EntityNotFound("AiProvider", request.Id, "Provedor não encontrado.");

        if (found.Data.IsDefault && await _repository.CountAsync() > 1)
            return RequestResult.WithDomainError("Escolha outro provedor como padrão antes de excluir este.");

        var deleted = await _repository.DeleteAsync(found.Data.Id);
        if (deleted.Status != EResultStatus.Success)
            return deleted;

        await _catalog.ReloadAsync();
        return RequestResult.Success();
    }
}
