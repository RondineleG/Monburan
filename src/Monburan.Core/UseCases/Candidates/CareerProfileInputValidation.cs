using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Requests;
using Monburan.Core.Services;

namespace Monburan.Core.UseCases.Candidates;

/// <summary>Formatos do perfil de carreira (contrato): cargo 120, cidade 100, UF 2 letras e modalidade conhecida.</summary>
internal static class CareerProfileInputValidation
{
    public static IReadOnlyList<RequestValidation> Validate(CareerProfileRequest request)
    {
        var errors = new List<RequestValidation>();

        if (request.DesiredRole?.Trim().Length > 120)
            errors.Add(new(nameof(request.DesiredRole), "O cargo desejado deve ter até 120 caracteres."));

        if (request.City?.Trim().Length > 100)
            errors.Add(new(nameof(request.City), "A cidade deve ter até 100 caracteres."));

        if (!string.IsNullOrWhiteSpace(request.State) && request.State.Trim().Length != 2)
            errors.Add(new(nameof(request.State), "A UF deve ter 2 letras."));

        if (!string.IsNullOrWhiteSpace(request.Mode) && JobModes.Normalize(request.Mode) is null)
            errors.Add(new(nameof(request.Mode), "Modalidade inválida. Use OnSite, Remote ou Hybrid."));

        if (request.Category?.Trim().Length > 100)
            errors.Add(new(nameof(request.Category), "A categoria deve ter até 100 caracteres."));

        if (request.MinimumSalary < 0)
            errors.Add(new(nameof(request.MinimumSalary), "O salário mínimo não pode ser negativo."));

        return errors;
    }
}
