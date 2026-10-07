namespace Monburan.Core.Validations;

/// <summary>
/// Validador de CPF (Cadastro de Pessoas Físicas).
/// Implementa a validação dos 11 dígitos e dígitos verificadores.
/// </summary>
public static class CpfValidator
{
    /// <summary>
    /// Valida se um CPF é válido.
    /// </summary>
    /// <param name="cpf">CPF com ou sem formatação (apenas dígitos)</param>
    /// <returns>True se o CPF for válido, False caso contrário</returns>
    public static bool IsValid(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        // Remover formatação (pontos e traço)
        cpf = cpf.Replace(".", "").Replace("-", "").Trim();

        // Validar apenas dígitos
        if (!IsAllDigits(cpf))
            return false;

        // CPF deve ter 11 dígitos
        if (cpf.Length != 11)
            return false;

        // CPF com todos os dígitos iguais é inválido (ex: 111.111.111-11)
        if (IsAllSameDigit(cpf))
            return false;

        // Validar primeiro dígito verificador
        var firstDigit = CalculateFirstDigit(cpf);
        if (firstDigit != int.Parse(cpf[9].ToString()))
            return false;

        // Validar segundo dígito verificador
        var secondDigit = CalculateSecondDigit(cpf);
        if (secondDigit != int.Parse(cpf[10].ToString()))
            return false;

        return true;
    }

    private static bool IsAllDigits(string cpf)
    {
        return cpf.All(char.IsDigit);
    }

    private static bool IsAllSameDigit(string cpf)
    {
        return cpf.Distinct().Count() == 1;
    }

    private static int CalculateFirstDigit(string cpf)
    {
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            sum += int.Parse(cpf[i].ToString()) * (10 - i);
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static int CalculateSecondDigit(string cpf)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += int.Parse(cpf[i].ToString()) * (11 - i);
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
