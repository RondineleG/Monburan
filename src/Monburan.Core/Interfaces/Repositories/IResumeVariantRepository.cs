using Monburan.Core.Entities;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

public interface IResumeVariantRepository
{
    Task<RequestResult<IEnumerable<ResumeVariant>>> ListByResumeAsync(Id resumeId, CancellationToken cancellationToken = default);

    Task<RequestResult<ResumeVariant?>> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    Task<RequestResult<ResumeVariant>> CreateAsync(ResumeVariant entity, CancellationToken cancellationToken = default);

    Task<RequestResult<ResumeVariant>> UpdateAsync(ResumeVariant entity, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete da variante (o currículo base nunca é tocado).</summary>
    Task<RequestResult<ResumeVariant?>> DeleteAsync(Id id, CancellationToken cancellationToken = default);
}
