using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.UnitTests.UseCases.Ai;

/// <summary>
/// Repositório em memória com as mesmas regras do real (exclusão lógica, nome sem diferenciar maiúsculas, um só padrão),
/// para os testes dos casos de uso. O repositório real tem seus próprios testes com o EF.
/// </summary>
internal sealed class FakeAiProviderRepository : IAiProviderRepository
{
    private readonly List<AiProvider> _items = [];

    public IReadOnlyList<AiProvider> Items => _items.Where(provider => !provider.IsDeleted).ToList();

    public int CreateCalls { get; private set; }

    public int UpdateCalls { get; private set; }

    public int SetDefaultCalls { get; private set; }

    public string? LastSetDefaultBy { get; private set; }

    public AiProvider Add(AiProvider provider)
    {
        _items.Add(provider);
        return provider;
    }

    public Task<RequestResult<AiProvider>> CreateAsync(AiProvider entity)
    {
        CreateCalls++;
        _items.Add(entity);
        return Task.FromResult(RequestResult<AiProvider>.Success(entity));
    }

    public Task<RequestResult<AiProvider>> DeleteAsync(Id id)
    {
        var provider = Find(id);
        if (provider is null)
            return Task.FromResult(RequestResult<AiProvider>.EntityNotFound("AiProvider", id.Value, "Provedor não encontrado."));

        provider.IsDeleted = true;
        provider.IsDefault = false;
        provider.IsEnabled = false;
        return Task.FromResult(RequestResult<AiProvider>.Success(provider));
    }

    public Task<RequestResult<IEnumerable<AiProvider>>> GetAllAsync(int page, int size) =>
        Task.FromResult(RequestResult<IEnumerable<AiProvider>>.Success(Items));

    public Task<RequestResult<AiProvider>> GetByIdAsync(Id id)
    {
        var provider = Find(id);
        return Task.FromResult(provider is null
            ? RequestResult<AiProvider>.EntityNotFound("AiProvider", id.Value, "Provedor não encontrado.")
            : RequestResult<AiProvider>.Success(provider));
    }

    public Task<RequestResult<AiProvider>> UpdateAsync(AiProvider entity)
    {
        UpdateCalls++;
        var provider = Find(entity.Id);
        return Task.FromResult(provider is null
            ? RequestResult<AiProvider>.EntityNotFound("AiProvider", entity.Id.Value, "Provedor não encontrado.")
            : RequestResult<AiProvider>.Success(provider));
    }

    public Task<RequestResult<IEnumerable<AiProvider>>> ListAsync() =>
        Task.FromResult(RequestResult<IEnumerable<AiProvider>>.Success(
            Items.OrderByDescending(provider => provider.IsDefault).ThenBy(provider => provider.Name).ToList()));

    public Task<bool> NameExistsAsync(string name, Id? exceptId = null) =>
        Task.FromResult(Items.Any(provider =>
            string.Equals(provider.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)
            && (exceptId is null || provider.Id != exceptId)));

    public Task<int> CountAsync() => Task.FromResult(Items.Count);

    public Task<RequestResult<AiProvider>> SetDefaultAsync(Id id, string updatedBy)
    {
        SetDefaultCalls++;
        LastSetDefaultBy = updatedBy;

        var target = Find(id);
        if (target is null)
            return Task.FromResult(RequestResult<AiProvider>.EntityNotFound("AiProvider", id.Value, "Provedor não encontrado."));

        foreach (var provider in Items)
            provider.IsDefault = provider.Id == id;

        return Task.FromResult(RequestResult<AiProvider>.Success(target));
    }

    private AiProvider? Find(Id id) => Items.FirstOrDefault(provider => provider.Id == id);
}
