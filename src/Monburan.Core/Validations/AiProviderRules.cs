using System.Text.RegularExpressions;

namespace Monburan.Core.Validations;

/// <summary>Regras dos provedores de IA configurados pelo administrador (limites e formato de nomes e endereços).</summary>
public static partial class AiProviderRules
{
    /// <summary>
    /// Toda variável de ambiente com chave de IA começa assim. É uma trava de segurança: sem ela, um administrador
    /// poderia apontar um provedor para <c>Jwt__SigningKey</c> ou a senha do banco e mandar esse segredo, no cabeçalho
    /// da requisição, para um endereço que ele controla.
    /// </summary>
    public const string EnvVarPrefix = "AI_KEY_";

    public const int NameMinLength = 2;
    public const int NameMaxLength = 80;
    public const int BaseUrlMaxLength = 300;
    public const int ModelMaxLength = 150;
    public const int EnvVarSuffixMaxLength = 60;
    public const int MinTimeoutSeconds = 5;
    public const int MaxTimeoutSeconds = 300;
    public const int MinOutputTokens = 256;
    public const int MaxOutputTokens = 32_000;
    public const double MinTemperature = 0;
    public const double MaxTemperature = 2;

    /// <summary><c>AI_KEY_</c> seguido de letras MAIÚSCULAS, números e sublinhado.</summary>
    public static bool IsValidEnvVarName(string? name) => !string.IsNullOrEmpty(name) && EnvVarName().IsMatch(name);

    /// <summary>
    /// Endereço absoluto em https (http só para o próprio computador, usado em testes e modelos locais), sem usuário,
    /// parâmetros nem fragmento. Quem decide se a rede de destino é permitida é a camada de conexão, não esta regra.
    /// </summary>
    public static bool TryParseBaseUrl(string? value, out Uri? uri, out string error)
    {
        uri = null;
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            error = "Informe o endereço do provedor.";
            return false;
        }

        if (text.Length > BaseUrlMaxLength)
        {
            error = $"O endereço deve ter no máximo {BaseUrlMaxLength} caracteres.";
            return false;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed) || string.IsNullOrEmpty(parsed.Host))
        {
            error = "Informe um endereço completo, como https://api.exemplo.com/v1.";
            return false;
        }

        var loopbackHttp = parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback;
        if (parsed.Scheme != Uri.UriSchemeHttps && !loopbackHttp)
        {
            error = "O endereço precisa começar com https:// (a chave de acesso não pode viajar sem criptografia).";
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            error = "O endereço não pode ter usuário nem senha; a chave vai por variável de ambiente.";
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment))
        {
            error = "O endereço não pode ter parâmetros (?) nem âncora (#).";
            return false;
        }

        uri = parsed;
        error = string.Empty;
        return true;
    }

    /// <summary>Endereço sem espaços nas pontas e sem barras no fim.</summary>
    public static string NormalizeBaseUrl(string? value) => (value ?? string.Empty).Trim().TrimEnd('/');

    [GeneratedRegex(@"^AI_KEY_[A-Z0-9_]{1,60}\z")]
    private static partial Regex EnvVarName();
}
