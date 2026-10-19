using Monburan.Core.Entities;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

/// <summary>Histórico de etapas da candidatura: uma linha por mudança de status.</summary>
public interface IApplicationStatusHistoryRepository
{
    Task<RequestResult<ApplicationStatusHistory>> CreateAsync(ApplicationStatusHistory entity);

    /// <summary>Linhas da candidatura, da mais antiga para a mais nova.</summary>
    Task<RequestResult<IEnumerable<ApplicationStatusHistory>>> ListByApplicationAsync(Guid applicationId);
}
