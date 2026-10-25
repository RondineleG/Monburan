using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

public interface IJobSubmissionRepository
{
    Task<RequestResult<JobSubmission>> CreateAsync(JobSubmission submission, CancellationToken cancellationToken = default);

    /// <summary>Carrega a sugestão com evidências e histórico.</summary>
    Task<RequestResult<JobSubmission>> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    Task<RequestResult<JobSubmission>> UpdateAsync(JobSubmission submission, CancellationToken cancellationToken = default);

    Task<RequestResult<PagedResponse<JobSubmission>>> ListAsync(ESubmissionStatus? status, int page, int size, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca itens parecidos (vagas publicadas e outras sugestões) pelos sinais disponíveis: empresa e título, cidade,
    /// endereço da fonte e hash da evidência.
    /// </summary>
    Task<RequestResult<IReadOnlyList<DuplicateCandidate>>> FindDuplicatesAsync(
        string title,
        string? companyName,
        string? city,
        string? sourceUrl,
        IReadOnlyCollection<string> evidenceHashes,
        Id? excludeSubmissionId,
        CancellationToken cancellationToken = default);
}
