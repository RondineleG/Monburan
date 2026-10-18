using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

public interface IAiProviderRepository : IBaseRepository<AiProvider>
{
    /// <summary>Provedores não excluídos, o padrão primeiro e depois por nome.</summary>
    Task<RequestResult<IEnumerable<AiProvider>>> ListAsync();

    /// <summary>Já existe outro provedor (não excluído) com esse nome, sem diferenciar maiúsculas?</summary>
    Task<bool> NameExistsAsync(string name, Id? exceptId = null);

    Task<int> CountAsync();

    /// <summary>Torna o provedor o padrão e tira a marca dos demais, de uma vez.</summary>
    Task<RequestResult<AiProvider>> SetDefaultAsync(Id id, string updatedBy);
}
