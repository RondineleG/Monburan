using Monburan.Core.Entities;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

/// <summary>Alertas de vaga do candidato. A checagem de dono é feita nos casos de uso.</summary>
public interface IJobAlertRepository
{
    Task<RequestResult<JobAlert>> CreateAsync(JobAlert entity, CancellationToken cancellationToken = default);

    Task<RequestResult<JobAlert>> DeleteAsync(Id id, CancellationToken cancellationToken = default);

    Task<RequestResult<JobAlert>> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    Task<RequestResult<JobAlert>> UpdateAsync(JobAlert entity, CancellationToken cancellationToken = default);

    /// <summary>Alertas do candidato, mais recentes primeiro.</summary>
    Task<RequestResult<IEnumerable<JobAlert>>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>Todos os alertas (não excluídos) do candidato — base do limite de 10.</summary>
    Task<RequestResult<int>> CountByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<RequestResult<int>> CountActiveByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>Alertas ativos de todos os candidatos: avaliados quando uma vaga nova é publicada.</summary>
    Task<RequestResult<IEnumerable<JobAlert>>> ListActiveAsync(CancellationToken cancellationToken = default);
}
