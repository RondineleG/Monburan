using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Monburan.Web.Services;

/// <summary>
/// Cookies de sessão só por HTTPS e sem acesso por script. Fora do desenvolvimento o atributo <c>Secure</c> é sempre
/// aplicado (o app roda atrás de TLS); em desenvolvimento acompanha a requisição, porque o ambiente local é HTTP.
/// </summary>
public sealed class CookieSecuritySetup(IHostEnvironment environment) : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private static readonly HashSet<string> IdentityCookies =
    [
        IdentityConstants.ApplicationScheme,
        IdentityConstants.ExternalScheme,
        IdentityConstants.TwoFactorRememberMeScheme,
        IdentityConstants.TwoFactorUserIdScheme
    ];

    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name is null || !IdentityCookies.Contains(name))
            return;

        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = SecurePolicyFor(environment);
    }

    public static CookieSecurePolicy SecurePolicyFor(IHostEnvironment environment) =>
        environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
}
