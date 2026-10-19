using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Ai;

/// <summary>
/// Na primeira execução (sem nenhum provedor cadastrado), cadastra os modelos marcados para isso: o Xiaomi MiMo, ativo e
/// como padrão, e os demais desativados, prontos para ativar quando a chave existir. Depois disso o administrador manda
/// em tudo pela tela; nada é recriado.
/// </summary>
public class SeedAiProvidersUseCase
{
    private readonly IAiProviderRepository _repository;
    private readonly IAiProviderCatalog _catalog;

    public SeedAiProvidersUseCase(IAiProviderRepository repository, IAiProviderCatalog catalog)
    {
        _repository = repository;
        _catalog = catalog;
    }

    /// <returns>Quantos provedores foram cadastrados (zero quando já havia algum).</returns>
    public async Task<int> ExecuteAsync(string createdBy = "sistema")
    {
        if (await _repository.CountAsync() > 0)
            return 0;

        var created = 0;
        foreach (var preset in AiProviderPresets.All.Where(item => item.SeedOnFirstRun))
        {
            var result = await _repository.CreateAsync(new AiProvider
            {
                Id = Id.Create(Guid.NewGuid()),
                TenantId = "default",
                Name = preset.Label,
                Protocol = preset.Protocol,
                BaseUrl = preset.BaseUrl,
                Model = preset.Model,
                ApiKeyEnvVar = preset.ApiKeyEnvVar,
                IsEnabled = preset.SeedAsDefault,
                IsDefault = preset.SeedAsDefault,
                UseMaxCompletionTokens = preset.UseMaxCompletionTokens,
                CreatedBy = createdBy,
                UpdatedBy = createdBy
            });

            if (result.Status == Enums.EResultStatus.Success)
                created++;
        }

        await _catalog.ReloadAsync();
        return created;
    }
}
