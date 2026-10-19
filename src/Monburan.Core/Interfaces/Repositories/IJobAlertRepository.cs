using Monburan.Core.Entities;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

/// <summary>Alertas de vaga do candidato. A checagem de dono é feita nos casos de uso.</summary>
public interface IJobAlertRepository
{
    Task<RequestResult<JobAlert>> CreateAsync(JobAlert entity);

    Task<RequestResult<JobAlert>> DeleteAsync(Id id);

    Task<RequestResult<JobAlert>> GetByIdAsync(Id id);

    Task<RequestResult<JobAlert>> UpdateAsync(JobAlert entity);

    /// <summary>Alertas do candidato, mais recentes primeiro.</summary>
    Task<RequestResult<IEnumerable<JobAlert>>> ListByCandidateAsync(Guid candidateId);

    /// <summary>Todos os alertas (não excluídos) do candidato — base do limite de 10.</summary>
    Task<RequestResult<int>> CountByCandidateAsync(Guid candidateId);

    Task<RequestResult<int>> CountActiveByCandidateAsync(Guid candidateId);

    /// <summary>Alertas ativos de todos os candidatos: avaliados quando uma vaga nova é publicada.</summary>
    Task<RequestResult<IEnumerable<JobAlert>>> ListActiveAsync();
}
