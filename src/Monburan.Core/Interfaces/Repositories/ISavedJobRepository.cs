using Monburan.Core.Entities;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

/// <summary>Vagas salvas do candidato. Salvar e dessalvar são idempotentes.</summary>
public interface ISavedJobRepository
{
    /// <summary>Salva a vaga; se já estava salva, devolve a linha existente sem duplicar.</summary>
    Task<RequestResult<SavedJob>> SaveAsync(SavedJob savedJob);

    /// <summary>Remove a vaga salva. Retorna <c>false</c> quando não havia nada para remover (sucesso também).</summary>
    Task<RequestResult<bool>> RemoveAsync(Guid candidateId, Guid jobId);

    /// <summary>Vagas salvas do candidato com a vaga (e a empresa) carregadas, mais recentes primeiro.</summary>
    Task<RequestResult<IEnumerable<SavedJob>>> ListByCandidateAsync(Guid candidateId);

    /// <summary>Ids, dentre os informados, que o candidato salvou — consulta em lote para as listagens.</summary>
    Task<RequestResult<IReadOnlyCollection<Guid>>> ListSavedJobIdsAsync(Guid candidateId, IReadOnlyCollection<Guid> jobIds);

    Task<RequestResult<int>> CountByCandidateAsync(Guid candidateId);
}
