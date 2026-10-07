namespace Monburan.Core.Validations;

/// <summary>
/// Validador de CNPJ (Cadastro Nacional da Pessoa Jurídica).
/// Implementa a validação dos 14 dígitos e dígitos verificadores.
/// </summary>
public static class CnpjValidator
{
    /// <summary>
    /// Valida se um CNPJ é válido.
    /// </summary>
    /// <param name="cnpj">CNPJ com ou sem formatação (apenas dígitos)</param>
    /// <returns>True se o CNPJ for válido, False caso contrário</returns>
    public static bool IsValid(string cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
            return false;

        // Remover formatação (pontos, barras e traço)
        cnpj = cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Trim();

        // Validar apenas dígitos
        if (!IsAllDigits(cnpj))
            return false;

        // CNPJ deve ter 14 dígitos
        if (cnpj.Length != 14)
            return false;

        // CNPJ com todos os dígitos iguais é inválido (ex: 11.111.111/1111-11)
        if (IsAllSameDigit(cnpj))
            return false;

        // Validar primeiro dígito verificador
        var firstDigit = CalculateFirstDigit(cnpj);
        if (firstDigit != int.Parse(cnpj[12].ToString()))
            return false;

        // Validar segundo dígito verificador
        var secondDigit = CalculateSecondDigit(cnpj);
        if (secondDigit != int.Parse(cnpj[13].ToString()))
            return false;

        return true;
    }

    private static bool IsAllDigits(string cnpj)
    {
        return cnpj.All(char.IsDigit);
    }

    private static bool IsAllSameDigit(string cnpj)
    {
        return cnpj.Distinct().Count() == 1;
    }

    private static int CalculateFirstDigit(string cnpj)
    {
        var weights = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum = 0;

        for (var i = 0; i < 12; i++)
        {
            sum += int.Parse(cnpj[i].ToString()) * weights[i];
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static int CalculateSecondDigit(string cnpj)
    {
        var weights = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum = 0;

        for (var i = 0; i < 13; i++)
        {
            sum += int.Parse(cnpj[i].ToString()) * weights[i];
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
