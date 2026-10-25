using Monburan.Core.Entities;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

public interface IResumeShareRepository
{
    Task<RequestResult<ResumeShare>> CreateAsync(ResumeShare share, CancellationToken cancellationToken = default);

    /// <summary>Busca pelo hash do token, já com o currículo e o candidato carregados.</summary>
    Task<RequestResult<ResumeShare>> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<RequestResult<ResumeShare>> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    Task<RequestResult<IReadOnlyList<ResumeShare>>> ListByResumeAsync(Id resumeId, CancellationToken cancellationToken = default);

    Task<RequestResult<ResumeShare>> UpdateAsync(ResumeShare share, CancellationToken cancellationToken = default);
}
