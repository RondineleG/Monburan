using Monburan.Core.DTOs.Request;

namespace Monburan.Web.Services;

/// <summary>Erro de validação de uma etapa do wizard de vagas.</summary>
public sealed record JobWizardStepError(string Field, string Message);

/// <summary>Validação por etapa do formulário de vaga (criar e editar). Regra pura, testada em unitário.</summary>
public static class JobWizardStepValidator
{
    public const int StepCount = 4;

    /// <summary>Valida só os campos da etapa atual; etapas fora de 1–4 não têm erro.</summary>
    public static IReadOnlyList<JobWizardStepError> Validate(int step, CreateJobRequest model) => step switch
    {
        1 => ValidateAbout(model),
        2 => ValidateDescription(model),
        3 => ValidateCompensation(model),
        _ => []
    };

    private static List<JobWizardStepError> ValidateAbout(CreateJobRequest model)
    {
        var errors = new List<JobWizardStepError>();

        if (string.IsNullOrWhiteSpace(model.Title))
            errors.Add(new(nameof(model.Title), "Informe o título da vaga."));
        else if (model.Title.Trim().Length > 200)
            errors.Add(new(nameof(model.Title), "O título deve ter até 200 caracteres."));

        if (model.Category?.Trim().Length > 100)
            errors.Add(new(nameof(model.Category), "A categoria deve ter até 100 caracteres."));

        if (model.Tags?.Trim().Length > 500)
            errors.Add(new(nameof(model.Tags), "As tags devem ter até 500 caracteres."));

        if (model.Type is not ("OnSite" or "Remote" or "Hybrid"))
            errors.Add(new(nameof(model.Type), "Escolha a modalidade da vaga."));

        if (model.Type != "Remote")
        {
            if (model.City?.Trim().Length > 100)
                errors.Add(new(nameof(model.City), "A cidade deve ter até 100 caracteres."));

            if (!string.IsNullOrWhiteSpace(model.State) && model.State.Trim().Length != 2)
                errors.Add(new(nameof(model.State), "A UF deve ter 2 letras."));
        }

        return errors;
    }

    private static List<JobWizardStepError> ValidateDescription(CreateJobRequest model)
    {
        var errors = new List<JobWizardStepError>();

        if (string.IsNullOrWhiteSpace(model.Description))
            errors.Add(new(nameof(model.Description), "Descreva a vaga."));
        else if (model.Description.Length > 2000)
            errors.Add(new(nameof(model.Description), "A descrição deve ter até 2000 caracteres."));

        if (model.Requirements?.Length > 2000)
            errors.Add(new(nameof(model.Requirements), "Os requisitos devem ter até 2000 caracteres."));

        if (model.Responsibilities?.Length > 2000)
            errors.Add(new(nameof(model.Responsibilities), "As responsabilidades devem ter até 2000 caracteres."));

        if (model.Benefits?.Length > 2000)
            errors.Add(new(nameof(model.Benefits), "Os benefícios devem ter até 2000 caracteres."));

        return errors;
    }

    private static List<JobWizardStepError> ValidateCompensation(CreateJobRequest model)
    {
        var errors = new List<JobWizardStepError>();

        if (model.SalaryMin is < 0)
            errors.Add(new(nameof(model.SalaryMin), "O salário mínimo não pode ser negativo."));

        if (model.SalaryMax is < 0)
            errors.Add(new(nameof(model.SalaryMax), "O salário máximo não pode ser negativo."));

        if (model.SalaryMin is { } min && model.SalaryMax is { } max && min > max)
            errors.Add(new(nameof(model.SalaryMax), "O salário máximo deve ser maior ou igual ao mínimo."));

        if (model.NumberOfOpenings < 1)
            errors.Add(new(nameof(model.NumberOfOpenings), "Informe ao menos 1 vaga."));

        if (model.ExpirationDate == default)
            errors.Add(new(nameof(model.ExpirationDate), "Informe a data de validade."));

        if (model.ApplicationInstructions?.Length > 2000)
            errors.Add(new(nameof(model.ApplicationInstructions), "As instruções devem ter até 2000 caracteres."));

        if (model.Observations?.Length > 2000)
            errors.Add(new(nameof(model.Observations), "As observações devem ter até 2000 caracteres."));

        return errors;
    }
}
