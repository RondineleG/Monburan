using System.Text.RegularExpressions;

namespace Monburan.Web.Middleware;

/// <summary>
/// Redireciona (301) as URLs antigas em inglês para as novas em português. Vale só para GET e HEAD: os POST
/// (sair, login externo, vincular login, exportar dados) continuam nos caminhos atuais. Links já enviados por
/// e-mail (confirmação e redefinição de senha) e favoritos seguem funcionando.
/// </summary>
public static class LegacyRouteRedirects
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

    private static readonly (Regex Pattern, string Replacement)[] Patterns =
    [
        (new Regex("^/jobs/edit/(?<id>[^/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/vagas/${id}/editar"),
        (new Regex("^/jobs/(?<id>[^/]+)/applications$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/vagas/${id}/candidaturas"),
        (new Regex("^/address/edit/(?<id>[^/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/enderecos/${id}/editar"),
        (new Regex("^/address/delete/(?<id>[^/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/enderecos/${id}/excluir"),
        (new Regex("^/resumes/(?<id>[^/]+)/wizard$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/curriculos/${id}/assistente"),
        (new Regex("^/resumes/(?<id>[^/]+)/edit$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/curriculos/${id}/editar"),
        (new Regex("^/resumes/(?<id>[^/]+)/cv$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/curriculos/${id}/cv"),
        (new Regex("^/resumes/(?<rest>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/curriculos/${rest}"),
        (new Regex("^/companies/(?<rest>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/empresas/${rest}"),
        (new Regex("^/apply/(?<rest>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "/candidatar/${rest}")
    ];

    /// <summary>Caminho novo para uma URL antiga, ou null quando a URL não mudou.</summary>
    public static string? TargetFor(string path)
    {
        if (Exact.TryGetValue(path, out var exact))
            return exact;

        foreach (var (pattern, replacement) in Patterns)
        {
            if (pattern.IsMatch(path))
                return pattern.Replace(path, replacement);
        }

        return null;
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
                context.Response.Redirect(target + context.Request.QueryString.Value, permanent: true);
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
