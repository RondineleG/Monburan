using Monburan.Core.Entities.Base;

namespace Monburan.Core.Interfaces.Validations;

public interface IEntityValidator<in T>
    where T : BaseEntity
{
    IValidationResult Validate(T entity);

    IValidationResult ValidateUpdate(T entity);
}