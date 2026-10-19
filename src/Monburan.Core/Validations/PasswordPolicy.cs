namespace Monburan.Core.Validations;

/// <summary>
/// Tamanho de senha da plataforma. Quem impõe a regra é o servidor (Identity); web, API e app só antecipam a mensagem
/// com este mesmo número, para o aviso nunca divergir do que o servidor aceita.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;

    public const int MaxLength = 100;

    public static string MinLengthMessage => $"A senha precisa ter pelo menos {MinLength} caracteres.";
}
