using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

/// <summary>
/// Consultas de candidaturas filtradas no banco — nunca carregar a tabela
/// inteira para filtrar em memória (dados somem quando a base cresce).
/// </summary>
public interface IApplicationRepository : IBaseRepository<Application>
{
    Task<RequestResult<IEnumerable<Application>>> ListByCandidateAsync(Guid candidateId);

    /// <summary>Candidaturas recebidas nas vagas do anunciante (opcionalmente de uma vaga).</summary>
    Task<RequestResult<IEnumerable<Application>>> ListByAdvertiserAsync(Guid advertiserId, Guid? jobId = null);

    /// <summary>Candidaturas do candidato agrupadas por situação, com a contagem feita no banco.</summary>
    Task<RequestResult<IReadOnlyDictionary<EApplicationStatus, int>>> CountByStatusForCandidateAsync(Guid candidateId);
}
