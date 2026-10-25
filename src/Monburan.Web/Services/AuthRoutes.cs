namespace Monburan.Web.Services;

/// <summary>
/// Páginas de entrada (login, cadastro, recuperação de senha, confirmação de e-mail). Elas usam o layout simples;
/// o restante da conta, inclusive "/conta/...", usa o shell normal do app.
/// </summary>
public static class AuthRoutes
{
    private static readonly string[] Prefixes =
    [
        "/entrar",
        "/criar-conta",
        "/esqueci-senha",
        "/redefinir-senha",
        "/confirmar-email",
        "/reenviar-confirmacao",
        "/acesso-negado",
        "/conta-bloqueada",
        "/conta/usuario-invalido"
    ];

    public static bool IsAuthPage(string path)
    {
        var clean = path.Split('?')[0].TrimEnd('/');
        return Prefixes.Any(prefix =>
            clean.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || clean.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
    }
}
