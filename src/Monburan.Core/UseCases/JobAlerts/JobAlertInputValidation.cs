using Monburan.Core.DTOs.Request;
using Monburan.Core.Requests;
using Monburan.Core.Services;

namespace Monburan.Core.UseCases.JobAlerts;

/// <summary>Limites e formatos do alerta (contrato): nome 1–80, palavras 200, cidade 100, UF 2 letras e modalidade conhecida.</summary>
internal static class JobAlertInputValidation
{
    public static IReadOnlyList<RequestValidation> Validate(JobAlertRequest request)
    {
        var errors = new List<RequestValidation>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add(new(nameof(request.Name), "Informe um nome para o alerta."));
        else if (request.Name.Trim().Length > 80)
            errors.Add(new(nameof(request.Name), "O nome do alerta deve ter até 80 caracteres."));

        if (request.Keywords?.Trim().Length > 200)
            errors.Add(new(nameof(request.Keywords), "As palavras-chave devem ter até 200 caracteres."));

        if (request.City?.Trim().Length > 100)
            errors.Add(new(nameof(request.City), "A cidade deve ter até 100 caracteres."));

        if (!string.IsNullOrWhiteSpace(request.State) && request.State.Trim().Length != 2)
            errors.Add(new(nameof(request.State), "A UF deve ter 2 letras."));

        if (!string.IsNullOrWhiteSpace(request.Mode) && JobModes.Normalize(request.Mode) is null)
            errors.Add(new(nameof(request.Mode), "Modalidade inválida. Use OnSite, Remote ou Hybrid."));

        if (request.Category?.Trim().Length > 100)
            errors.Add(new(nameof(request.Category), "A categoria deve ter até 100 caracteres."));

        if (request.SalaryMin < 0)
            errors.Add(new(nameof(request.SalaryMin), "O salário mínimo não pode ser negativo."));

        return errors;
    }
}
