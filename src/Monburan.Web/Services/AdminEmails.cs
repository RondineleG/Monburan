namespace Monburan.Web.Services;

/// <summary>Os e-mails de administradores definidos por configuração (<c>Admin:Emails</c>, separados por vírgula ou ponto e vírgula).</summary>
public static class AdminEmails
{
    public static IReadOnlyList<string> Read(IConfiguration configuration) => Parse(configuration["Admin:Emails"]);

    public static IReadOnlyList<string> Parse(string? configured) =>
        string.IsNullOrWhiteSpace(configured)
            ? []
            : configured.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(email => email.ToLowerInvariant())
                .Distinct()
                .ToList();

    /// <summary>Só vale com o e-mail confirmado: sem isso bastaria cadastrar o e-mail de outra pessoa.</summary>
    public static bool IsConfiguredAdmin(ApplicationUser user, IReadOnlyCollection<string> configured) =>
        user.EmailConfirmed && !string.IsNullOrWhiteSpace(user.Email) && configured.Contains(user.Email.ToLowerInvariant());
}
