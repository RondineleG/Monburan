using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Validations;

namespace Monburan.Core.Validations;

public class JobValidator : IEntityValidator<Job>
{
    public IValidationResult Validate(Job entity)
    {
        var result = new ValidationResult();

        if (entity.AdvertiserId == null || entity.AdvertiserId.Value == Guid.Empty)
            result.AddError("Anunciante é obrigatório.", "AdvertiserId");

        if (string.IsNullOrWhiteSpace(entity.Title))
            result.AddError("Título é obrigatório.", "Title");
        else if (entity.Title.Length < 3 || entity.Title.Length > 200)
            result.AddError("Título deve ter entre 3 e 200 caracteres.", "Title");

        if (string.IsNullOrWhiteSpace(entity.Description))
            result.AddError("Descrição é obrigatória.", "Description");
        else if (entity.Description.Length < 10 || entity.Description.Length > 2000)
            result.AddError("Descrição deve ter entre 10 e 2000 caracteres.", "Description");

        if (entity.ExpirationDate <= DateTime.UtcNow)
            result.AddError("Data de expiração deve ser futura.", "ExpirationDate");

        if (entity.NumberOfOpenings < 1)
            result.AddError("Informe ao menos uma vaga disponível.", "NumberOfOpenings");

        if (!string.IsNullOrEmpty(entity.Observations) && entity.Observations.Length > 2000)
            result.AddError("Observações devem ter no máximo 2000 caracteres.", "Observations");

        if (!string.IsNullOrEmpty(entity.ApplicationInstructions) && entity.ApplicationInstructions.Length > 2000)
            result.AddError("As instruções para candidatura devem ter no máximo 2000 caracteres.", "ApplicationInstructions");

        if (entity.Salary != null && entity.Salary.Min.HasValue && entity.Salary.Max.HasValue && entity.Salary.Min > entity.Salary.Max)
            result.AddError("Salário mínimo deve ser menor ou igual ao salário máximo.", "Salary");

        if (!string.IsNullOrEmpty(entity.Requirements) && entity.Requirements.Length > 2000)
            result.AddError("Requisitos deve ter no máximo 2000 caracteres.", "Requirements");

        if (!string.IsNullOrEmpty(entity.Responsibilities) && entity.Responsibilities.Length > 2000)
            result.AddError("Responsabilidades deve ter no máximo 2000 caracteres.", "Responsibilities");

        if (!string.IsNullOrEmpty(entity.Benefits) && entity.Benefits.Length > 2000)
            result.AddError("Benefícios deve ter no máximo 2000 caracteres.", "Benefits");

        if (!string.IsNullOrEmpty(entity.Category) && entity.Category.Length > 100)
            result.AddError("Categoria deve ter no máximo 100 caracteres.", "Category");

        if (!string.IsNullOrEmpty(entity.Tags) && entity.Tags.Length > 500)
            result.AddError("Tags deve ter no máximo 500 caracteres.", "Tags");

        return result;
    }

    public IValidationResult ValidateUpdate(Job entity)
    {
        return Validate(entity);
    }
}
