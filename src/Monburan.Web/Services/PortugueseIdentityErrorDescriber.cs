namespace Monburan.Web.Services;

/// <summary>
/// Mensagens do ASP.NET Identity em pt-BR. Sem isto, cadastro e troca de senha
/// exibiam "Passwords must have at least one uppercase ('A'-'Z')." no site e no app.
/// </summary>
public sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Não foi possível concluir a operação. Tente novamente.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Os dados foram alterados por outra sessão. Recarregue a página e tente novamente.");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "Senha incorreta.");

    public override IdentityError InvalidToken() =>
        Error(nameof(InvalidToken), "O link utilizado é inválido ou expirou. Solicite um novo.");

    public override IdentityError RecoveryCodeRedemptionFailed() =>
        Error(nameof(RecoveryCodeRedemptionFailed), "Código de recuperação inválido.");

    public override IdentityError LoginAlreadyAssociated() =>
        Error(nameof(LoginAlreadyAssociated), "Já existe uma conta vinculada a este login externo.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "Nome de usuário inválido. Use apenas letras, números e os caracteres -._@+.");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), "Informe um e-mail válido.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Já existe uma conta com este e-mail.");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), "Já existe uma conta com este e-mail.");

    public override IdentityError InvalidRoleName(string? role) =>
        Error(nameof(InvalidRoleName), "Perfil de acesso inválido.");

    public override IdentityError DuplicateRoleName(string role) =>
        Error(nameof(DuplicateRoleName), "Este perfil de acesso já existe.");

    public override IdentityError UserAlreadyHasPassword() =>
        Error(nameof(UserAlreadyHasPassword), "Esta conta já possui senha definida.");

    public override IdentityError UserLockoutNotEnabled() =>
        Error(nameof(UserLockoutNotEnabled), "O bloqueio temporário não está habilitado para esta conta.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Error(nameof(UserAlreadyInRole), "A conta já possui este perfil de acesso.");

    public override IdentityError UserNotInRole(string role) =>
        Error(nameof(UserNotInRole), "A conta não possui este perfil de acesso.");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"A senha deve ter pelo menos {length} caracteres.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), $"A senha deve ter pelo menos {uniqueChars} caracteres diferentes.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "A senha deve ter pelo menos um caractere especial (ex.: ! @ #).");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "A senha deve ter pelo menos um número (0-9).");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "A senha deve ter pelo menos uma letra minúscula (a-z).");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "A senha deve ter pelo menos uma letra maiúscula (A-Z).");

    private static IdentityError Error(string code, string description) =>
        new() { Code = code, Description = description };
}
