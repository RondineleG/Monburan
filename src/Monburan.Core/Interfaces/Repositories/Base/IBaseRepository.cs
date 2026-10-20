using Monburan.Core.Entities.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories.Base;

public interface IBaseRepository<TEntity>
    where TEntity : BaseEntity
{
    Task<RequestResult<TEntity>> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<RequestResult<TEntity>> DeleteAsync(Id id, CancellationToken cancellationToken = default);

    Task<RequestResult<IEnumerable<TEntity>>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default);

    Task<RequestResult<TEntity>> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    Task<RequestResult<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
}