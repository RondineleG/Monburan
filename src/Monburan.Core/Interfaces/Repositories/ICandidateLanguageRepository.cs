using Monburan.Core.Entities;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

public interface ICandidateLanguageRepository
{
    /// <summary>Idiomas do candidato, ordenados por tag — sem dados quando o perfil está vazio.</summary>
    Task<RequestResult<IEnumerable<CandidateLanguage>>> GetByCandidateIdAsync(Id candidateId, CancellationToken cancellationToken = default);

    /// <summary>Substitui o conjunto inteiro de idiomas do candidato numa única transação.</summary>
    Task<RequestResult<IEnumerable<CandidateLanguage>>> ReplaceForCandidateAsync(
        Id candidateId, IReadOnlyList<CandidateLanguage> languages, CancellationToken cancellationToken = default);
}
