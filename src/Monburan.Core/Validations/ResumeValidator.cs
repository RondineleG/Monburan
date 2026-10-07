using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Validations;

namespace Monburan.Core.Validations;

public class ResumeValidator : IEntityValidator<Resume>
{
    public IValidationResult Validate(Resume entity)
    {
        var result = new ValidationResult();

        if (entity.CandidateId == null || entity.CandidateId.Value == Guid.Empty)
            result.AddError("Candidato é obrigatório.", "CandidateId");

        if (string.IsNullOrWhiteSpace(entity.Title))
            result.AddError("Título é obrigatório.", "Title");
        else if (entity.Title.Length < 3 || entity.Title.Length > 100)
            result.AddError("Título deve ter entre 3 e 100 caracteres.", "Title");

        if (!string.IsNullOrEmpty(entity.Summary) && entity.Summary.Length > 2000)
            result.AddError("Resumo deve ter no máximo 2000 caracteres.", "Summary");

        return result;
    }

    public IValidationResult ValidateUpdate(Resume entity)
    {
        return Validate(entity);
    }
}
