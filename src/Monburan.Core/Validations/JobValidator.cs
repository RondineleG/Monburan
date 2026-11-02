using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Validations;

namespace Monburan.Core.Validations;

public class JobValidator : IEntityValidator<Job>
{
    public IValidationResult Validate(Job entity)
    {
        var result = new ValidationResult();

        ValidatePublication(entity, result);

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

        ValidateApplicationChannel(entity, result);

        return result;
    }

    private static void ValidatePublication(Job entity, ValidationResult result)
    {
        if (entity.PublicationType == EPublicationType.Direct)
        {
            if (entity.AdvertiserId == null || entity.AdvertiserId.Value == Guid.Empty)
                result.AddError("Anunciante é obrigatório.", "AdvertiserId");

            if (entity.ApplicationChannel != EApplicationChannel.InternalPlatform)
                result.AddError("Vaga direta recebe candidaturas pela Monburan.", "ApplicationChannel");

            return;
        }

        if (entity.AdvertiserId != null)
            result.AddError("Vaga externa não tem anunciante: a empresa contratante vai em CompanyName.", "AdvertiserId");

        if (string.IsNullOrWhiteSpace(entity.CompanyName))
            result.AddError("Informe a empresa contratante da vaga externa.", "CompanyName");

        if (string.IsNullOrWhiteSpace(entity.SourceName) && string.IsNullOrWhiteSpace(entity.SourceUrl))
            result.AddError("Informe a fonte da vaga externa (nome ou endereço).", "SourceName");

        if (entity.ApplicationChannel == EApplicationChannel.InternalPlatform)
            result.AddError("Vaga externa não recebe candidaturas pela Monburan.", "ApplicationChannel");
    }

    private static void ValidateApplicationChannel(Job entity, ValidationResult result)
    {
        switch (entity.ApplicationChannel)
        {
            case EApplicationChannel.ExternalUrl:
                if (!Uri.TryCreate(entity.ApplicationUrl, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
                    result.AddError("Informe o endereço (http ou https) do site para a candidatura.", "ApplicationUrl");
                break;

            case EApplicationChannel.WhatsApp:
                if (string.IsNullOrWhiteSpace(entity.ApplicationContact) || entity.ApplicationContact.Count(char.IsDigit) < 11)
                    result.AddError("Informe o número de WhatsApp com DDD e o celular de 9 dígitos.", "ApplicationContact");
                break;

            case EApplicationChannel.Email:
                if (string.IsNullOrWhiteSpace(entity.ApplicationContact) || !System.Net.Mail.MailAddress.TryCreate(entity.ApplicationContact, out _))
                    result.AddError("Informe um e-mail válido para receber o currículo.", "ApplicationContact");
                break;

            case EApplicationChannel.OnSite:
                if (string.IsNullOrWhiteSpace(entity.ApplicationInstructions))
                    result.AddError("Informe o local e as instruções para a candidatura presencial.", "ApplicationInstructions");
                break;
        }
    }

    public IValidationResult ValidateUpdate(Job entity)
    {
        return Validate(entity);
    }
}
