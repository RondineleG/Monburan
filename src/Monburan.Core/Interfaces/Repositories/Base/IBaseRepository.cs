using Monburan.Core.Entities.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories.Base;

public interface IBaseRepository<TEntity>
    where TEntity : BaseEntity
{
    Task<RequestResult<TEntity>> CreateAsync(TEntity entity);

    Task<RequestResult<TEntity>> DeleteAsync(Id id);

    Task<RequestResult<IEnumerable<TEntity>>> GetAllAsync(int page, int size);

    Task<RequestResult<TEntity>> GetByIdAsync(Id id);

    Task<RequestResult<TEntity>> UpdateAsync(TEntity entity);
}