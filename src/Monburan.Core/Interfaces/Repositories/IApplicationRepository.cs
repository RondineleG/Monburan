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
    /// <summary>
    /// Candidaturas do candidato. <paramref name="size"/> em 0 devolve até o teto da listagem
    /// (<see cref="MaxListSize"/>); com tamanho maior, devolve a página pedida.
    /// </summary>
    Task<RequestResult<IEnumerable<Application>>> ListByCandidateAsync(Guid candidateId, int page = 1, int size = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Candidaturas recebidas nas vagas do anunciante (opcionalmente de uma vaga).
    /// <paramref name="size"/> em 0 devolve até o teto da listagem (<see cref="MaxListSize"/>).
    /// </summary>
    Task<RequestResult<IEnumerable<Application>>> ListByAdvertiserAsync(Guid advertiserId, Guid? jobId = null, int page = 1, int size = 0, CancellationToken cancellationToken = default);

    /// <summary>Candidaturas do candidato agrupadas por situação, com a contagem feita no banco.</summary>
    Task<RequestResult<IReadOnlyDictionary<EApplicationStatus, int>>> CountByStatusForCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A candidatura do candidato em uma vaga específica (ou nulo). Consulta exata no banco,
    /// sem teto de listagem: nunca "adivinhar" pela página carregada.
    /// </summary>
    Task<RequestResult<Application?>> FindByCandidateAndJobAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// O anunciante só lê o currículo quando ele foi anexado a uma candidatura deste candidato
    /// em uma vaga dele. Consulta exata (AnyAsync): não passa pela listagem com teto.
    /// </summary>
    Task<bool> CanAdvertiserViewResumeAsync(Guid advertiserId, Guid resumeId, Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maior quantidade que uma listagem devolve sem paginação. Uma empresa com dezenas de
    /// milhares de candidaturas não pode materializar tudo em memória a cada requisição.
    /// </summary>
    const int MaxListSize = 200;
}
