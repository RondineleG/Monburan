using Monburan.Core.Validations;

namespace Monburan.Web.Services;

/// <summary>
/// Barra as senhas que cumprem as regras de tamanho e de caracteres mas são as primeiras que qualquer um tenta:
/// as mais comuns, o próprio e-mail ou nome de usuário, um caractere repetido e sequências óbvias. É uma linha de
/// base contra ataque de dicionário; as regras de tamanho e de caracteres continuam com o Identity.
/// </summary>
public sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    // Comparadas sem diferenciar maiúsculas e sem símbolos, só letras e números ("Senha@12345" vale "senha12345")
    private static readonly HashSet<string> Common = new(StringComparer.Ordinal)
    {
        "password", "password1", "password12", "password123", "password1234", "password12345", "passw0rd", "passw0rd123",
        "senha", "senha123", "senha1234", "senha12345", "senha123456", "senhaforte", "senhasegura", "minhasenha",
        "minhasenha123", "mudar123", "mudar1234", "mudar12345", "mudarsenha", "trocar123", "trocar1234", "trocar12345",
        "qwerty", "qwerty123", "qwerty1234", "qwerty12345", "qwertyuiop", "qwertyuiop1", "asdfghjkl", "asdfghjkl1",
        "abc123", "abc1234", "abc12345", "abc123456", "abc1234567", "abcd1234", "abcd12345", "abcd123456",
        "admin", "admin123", "admin1234", "admin12345", "administrador", "administrator1", "root1234", "root12345",
        "welcome", "welcome1", "welcome123", "welcome1234", "bemvindo", "bemvindo123", "bemvindo1234", "letmein123",
        "iloveyou", "iloveyou1", "iloveyou123", "teste123", "teste1234", "teste12345", "test1234", "test12345",
        "changeme", "changeme123", "changeme1234", "football123", "futebol123", "brasil123", "brasil1234", "brasil12345",
        "brasil2024", "brasil2025", "brasil2026", "flamengo123", "corinthians1", "corinthians123", "palmeiras123",
        "123456789a", "1234567890a", "a123456789", "a1234567890", "1q2w3e4r5t", "1q2w3e4r5t6y", "q1w2e3r4t5", "q1w2e3r4t5y6",
        "monburan123456", "recrutamento123"
    };

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
            return Task.FromResult(IdentityResult.Success); // "campo vazio" é problema das outras regras

        var normalized = Normalize(password);

        if (Common.Contains(normalized))
            return Fail("PasswordTooCommon", "Esta senha é muito comum e fácil de adivinhar. Escolha outra.");

        if (SameAsAccount(normalized, user))
            return Fail("PasswordSameAsAccount", "A senha não pode ser igual ao seu e-mail ou nome de usuário.");

        if (IsRepeatedOrSequence(normalized))
            return Fail("PasswordTooSimple", "A senha é simples demais (caracteres repetidos ou em sequência). Misture letras, números e palavras.");

        return Task.FromResult(IdentityResult.Success);
    }

    private static string Normalize(string password) =>
        new(password.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool SameAsAccount(string normalizedPassword, ApplicationUser user)
    {
        foreach (var value in new[] { user.Email, user.UserName })
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var normalizedValue = Normalize(value);
            var local = Normalize(value.Split('@')[0]);
            if (normalizedValue.Length > 0 && normalizedPassword == normalizedValue)
                return true;

            // O começo do e-mail com um número no fim ("joao.silva@..." -> "joaosilva1234") também é adivinhação óbvia
            if (local.Length >= 5 && normalizedPassword.StartsWith(local, StringComparison.Ordinal)
                && normalizedPassword[local.Length..].All(char.IsDigit))
                return true;
        }

        return false;
    }

    private static bool IsRepeatedOrSequence(string normalized)
    {
        if (normalized.Length == 0)
            return false;

        if (normalized.Distinct().Count() <= 2)
            return true;

        const string digits = "01234567890123456789";
        const string letters = "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz";
        return digits.Contains(normalized, StringComparison.Ordinal)
            || letters.Contains(normalized, StringComparison.Ordinal)
            || Reverse(digits).Contains(normalized, StringComparison.Ordinal);
    }

    private static string Reverse(string value) => new(value.Reverse().ToArray());

    private static Task<IdentityResult> Fail(string code, string description) =>
        Task.FromResult(IdentityResult.Failed(new IdentityError { Code = code, Description = description }));
}
