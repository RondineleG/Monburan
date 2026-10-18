using System.Text.RegularExpressions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;

namespace Monburan.Core.Validations;

/// <summary>Valida o cadastro de um provedor de IA. Devolve (campo, mensagem) para cada problema.</summary>
public static partial class AiProviderValidator
{
    public static List<(string Field, string Message)> Validate(AiProviderRequest request)
    {
        var errors = new List<(string, string)>();

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length < AiProviderRules.NameMinLength || name.Length > AiProviderRules.NameMaxLength)
            errors.Add((nameof(request.Name), $"O nome deve ter entre {AiProviderRules.NameMinLength} e {AiProviderRules.NameMaxLength} caracteres."));
        else if (name.Any(char.IsControl))
            errors.Add((nameof(request.Name), "O nome não pode ter caracteres de controle."));

        if (!Enum.IsDefined(request.Protocol))
            errors.Add((nameof(request.Protocol), "Escolha o protocolo do provedor."));

        if (!AiProviderRules.TryParseBaseUrl(request.BaseUrl, out _, out var urlError))
            errors.Add((nameof(request.BaseUrl), urlError));

        var model = request.Model?.Trim() ?? string.Empty;
        if (model.Length == 0)
            errors.Add((nameof(request.Model), "Informe o modelo."));
        else if (model.Length > AiProviderRules.ModelMaxLength || !ModelName().IsMatch(model))
            errors.Add((nameof(request.Model), "O modelo só pode ter letras, números e . _ - : / @ + (sem espaços)."));

        if (!AiProviderRules.IsValidEnvVarName(request.ApiKeyEnvVar?.Trim()))
        {
            errors.Add((nameof(request.ApiKeyEnvVar),
                $"A variável deve começar com {AiProviderRules.EnvVarPrefix} e usar só letras MAIÚSCULAS, números e sublinhado (ex.: {AiProviderRules.EnvVarPrefix}OPENROUTER)."));
        }

        if (request.TimeoutSeconds < AiProviderRules.MinTimeoutSeconds || request.TimeoutSeconds > AiProviderRules.MaxTimeoutSeconds)
            errors.Add((nameof(request.TimeoutSeconds), $"O tempo limite deve ficar entre {AiProviderRules.MinTimeoutSeconds} e {AiProviderRules.MaxTimeoutSeconds} segundos."));

        if (request.MaxOutputTokens < AiProviderRules.MinOutputTokens || request.MaxOutputTokens > AiProviderRules.MaxOutputTokens)
            errors.Add((nameof(request.MaxOutputTokens), $"O limite de tokens deve ficar entre {AiProviderRules.MinOutputTokens} e {AiProviderRules.MaxOutputTokens}."));

        if (request.SendTemperature
            && (double.IsNaN(request.Temperature) || request.Temperature < AiProviderRules.MinTemperature || request.Temperature > AiProviderRules.MaxTemperature))
            errors.Add((nameof(request.Temperature), "A temperatura deve ficar entre 0 e 2."));

        return errors;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._\-:/@+]+$")]
    private static partial Regex ModelName();
}
