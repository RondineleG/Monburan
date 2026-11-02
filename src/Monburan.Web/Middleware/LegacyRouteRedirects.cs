using System.Text.RegularExpressions;

namespace Monburan.Web.Middleware;

/// <summary>
/// Redireciona (301) as URLs antigas em inglês para as novas em português. Vale só para GET e HEAD: os POST
/// (sair, login externo, vincular login, exportar dados) continuam nos caminhos atuais. Links já enviados por
/// e-mail (confirmação e redefinição de senha) e favoritos seguem funcionando.
/// </summary>
public static partial class LegacyRouteRedirects
{
    private static readonly Dictionary<string, string> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/Account/Login"] = "/entrar",
        ["/Account/LoginWith2fa"] = "/entrar/2fa",
        ["/Account/LoginWithRecoveryCode"] = "/entrar/codigo-recuperacao",
        ["/Account/ExternalLogin"] = "/entrar/externo",
        ["/Account/Register"] = "/criar-conta",
        ["/Account/RegisterConfirmation"] = "/criar-conta/confirmacao",
        ["/Account/ForgotPassword"] = "/esqueci-senha",
        ["/Account/ForgotPasswordConfirmation"] = "/esqueci-senha/enviado",
        ["/Account/ResetPassword"] = "/redefinir-senha",
        ["/Account/ResetPasswordConfirmation"] = "/redefinir-senha/concluido",
        ["/Account/InvalidPasswordReset"] = "/redefinir-senha/invalido",
        ["/Account/ConfirmEmail"] = "/confirmar-email",
        ["/Account/ConfirmEmailChange"] = "/confirmar-email/alteracao",
        ["/Account/ResendEmailConfirmation"] = "/reenviar-confirmacao",
        ["/Account/InvalidUser"] = "/conta/usuario-invalido",
        ["/Account/Lockout"] = "/conta-bloqueada",
        ["/Account/AccessDenied"] = "/acesso-negado",
        ["/Account/Manage"] = "/conta",
        ["/Account/Manage/TwoFactorAuthentication"] = "/conta/seguranca-2fa",
        ["/Account/Manage/Email"] = "/conta/email",
        ["/Account/Manage/ChangePassword"] = "/conta/alterar-senha",
        ["/Account/Manage/PersonalData"] = "/conta/dados-pessoais",
        ["/Account/Manage/ExternalLogins"] = "/conta/logins-externos",
        ["/Account/Manage/Disable2fa"] = "/conta/desativar-2fa",
        ["/Account/Manage/EnableAuthenticator"] = "/conta/ativar-autenticador",
        ["/Account/Manage/GenerateRecoveryCodes"] = "/conta/codigos-recuperacao",
        ["/Account/Manage/ResetAuthenticator"] = "/conta/redefinir-autenticador",
        ["/Account/Manage/SetPassword"] = "/conta/definir-senha",
        ["/Account/Manage/DeletePersonalData"] = "/conta/excluir-dados",
        ["/dashboard"] = "/painel",
        ["/my-applications"] = "/minhas-candidaturas",
        ["/my-jobs"] = "/minhas-vagas",
        ["/my-subscription"] = "/minha-assinatura",
        ["/received-applications"] = "/candidaturas-recebidas",
        ["/career-profile"] = "/perfil-profissional",
        ["/profile/candidate"] = "/perfil/candidato",
        ["/profile/advertiser"] = "/perfil/empresa",
        ["/advertiser/upload-logo"] = "/empresa/logo",
        ["/jobs/create"] = "/vagas/nova",
        ["/address/list"] = "/enderecos",
        ["/address/create"] = "/enderecos/novo",
        ["/resumes"] = "/curriculos",
        ["/resumes/new"] = "/curriculos/novo",
        ["/resumes/upload"] = "/curriculos/upload",
        ["/resumes/wizard"] = "/curriculos/assistente",
        ["/admin/accounts"] = "/admin/contas",
        ["/admin/advertisers"] = "/admin/empresas",
        ["/admin/candidates"] = "/admin/candidatos",
        ["/admin/jobs"] = "/admin/vagas",
        ["/admin/audit"] = "/admin/auditoria",
        ["/admin/ai-providers"] = "/admin/provedores-ia",
        ["/privacy"] = "/privacidade",
        ["/terms"] = "/termos"
    };

    // Rotas com parâmetro. [GeneratedRegex] compila no build: sem custo de construção nem de JIT na primeira requisição, e
    // a correspondência é mais rápida que a do Regex interpretado. Cada regra captura o trecho variável no grupo "v";
    // o destino é prefixo + v + sufixo, montado a partir de um único Match (antes eram IsMatch seguido de Replace, duas
    // varreduras do mesmo texto).
    [GeneratedRegex("^/jobs/edit/(?<v>[^/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JobEdit();

    [GeneratedRegex("^/jobs/(?<v>[^/]+)/applications$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JobApplications();

    [GeneratedRegex("^/address/edit/(?<v>[^/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AddressEdit();

    [GeneratedRegex("^/address/delete/(?<v>[^/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AddressDelete();

    [GeneratedRegex("^/resumes/(?<v>[^/]+)/wizard$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResumeWizard();

    [GeneratedRegex("^/resumes/(?<v>[^/]+)/edit$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResumeEdit();

    [GeneratedRegex("^/resumes/(?<v>[^/]+)/cv$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResumeCv();

    [GeneratedRegex("^/resumes/(?<v>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResumeRest();

    [GeneratedRegex("^/companies/(?<v>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompanyRest();

    [GeneratedRegex("^/apply/(?<v>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ApplyRest();

    // A ordem importa: as regras específicas de /resumes vêm antes da genérica
    private static readonly (Regex Pattern, string Prefix, string Suffix)[] Patterns =
    [
        (JobEdit(), "/vagas/", "/editar"),
        (JobApplications(), "/vagas/", "/candidaturas"),
        (AddressEdit(), "/enderecos/", "/editar"),
        (AddressDelete(), "/enderecos/", "/excluir"),
        (ResumeWizard(), "/curriculos/", "/assistente"),
        (ResumeEdit(), "/curriculos/", "/editar"),
        (ResumeCv(), "/curriculos/", "/cv"),
        (ResumeRest(), "/curriculos/", string.Empty),
        (CompanyRest(), "/empresas/", string.Empty),
        (ApplyRest(), "/candidatar/", string.Empty)
    ];

    /// <summary>Caminho novo para uma URL antiga, ou null quando a URL não mudou. O caminho vem decodificado.</summary>
    public static string? TargetFor(string path)
    {
        if (Exact.TryGetValue(path, out var exact))
            return exact;

        // Só 5 primeiros segmentos têm rota com parâmetro. Todo o resto (CSS, JS, imagens, Blazor, API) sai aqui, sem
        // passar por nenhuma regex: este código roda em toda requisição, antes dos arquivos estáticos
        if (!MayHaveParameterizedRoute(path))
            return null;

        foreach (var (pattern, prefix, suffix) in Patterns)
        {
            var match = pattern.Match(path);
            if (match.Success)
                return string.Concat(prefix, match.Groups["v"].ValueSpan, suffix);
        }

        return null;
    }

    private static bool MayHaveParameterizedRoute(string path)
    {
        if (path.Length < 2 || path[0] != '/')
            return false;

        var span = path.AsSpan(1);
        var slash = span.IndexOf('/');
        var first = slash < 0 ? span : span[..slash];
        return first.Equals("jobs", StringComparison.OrdinalIgnoreCase)
            || first.Equals("address", StringComparison.OrdinalIgnoreCase)
            || first.Equals("resumes", StringComparison.OrdinalIgnoreCase)
            || first.Equals("companies", StringComparison.OrdinalIgnoreCase)
            || first.Equals("apply", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class LegacyRouteRedirectMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
        {
            var target = LegacyRouteRedirects.TargetFor(context.Request.Path.Value ?? string.Empty);
            if (target is not null)
            {
                // Request.Path chega decodificado: um %3F virava "?" e mudava a URL de destino. ToUriComponent() codifica
                // de novo o que precisa (?, #, %, espaço), e a query string original segue intacta
                var location = new PathString(target).ToUriComponent() + context.Request.QueryString.Value;
                context.Response.Redirect(location, permanent: true);
                return Task.CompletedTask;
            }
        }

        return next(context);
    }
}

public static class LegacyRouteRedirectMiddlewareExtensions
{
    public static IApplicationBuilder UseLegacyRouteRedirects(this IApplicationBuilder app) =>
        app.UseMiddleware<LegacyRouteRedirectMiddleware>();
}
