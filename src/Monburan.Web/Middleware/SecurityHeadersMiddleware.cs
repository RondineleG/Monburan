using Microsoft.Extensions.Options;

using Monburan.Web.Services;

namespace Monburan.Web.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<SecurityOptions> _options;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptionsMonitor<SecurityOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        // O filtro XSS dos navegadores antigos foi removido por abrir brechas; o CSP é a defesa de hoje
        headers["X-XSS-Protection"] = "0";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=(), usb=()";

        // CSP_REPORT_ONLY (Security__CspReportOnly) deixa o navegador só avisar no console, sem bloquear: válvula de emergência
        // caso uma política nova quebre alguma página em produção
        var headerName = _options.CurrentValue.CspReportOnly ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy";
        headers[headerName] = ContentSecurityPolicy(context.Request.Host.Value, context.Request.IsHttps);

        await _next(context);
    }

    /// <summary>
    /// Política de conteúdo: scripts só do próprio site (nada inline, nada de eval); folhas de estilo e fontes só do próprio site
    /// (o atributo style continua permitido, porque o Blazor e alguns componentes dependem dele); sem plugins,
    /// sem trocar a base dos links, formulários só para o próprio site e a página nunca dentro de um frame.
    /// O WebSocket do Blazor é liberado de forma explícita para o próprio host, porque nem todo navegador entende que
    /// <c>'self'</c> cobre ws/wss.
    /// </summary>
    public static string ContentSecurityPolicy(string? host, bool isHttps)
    {
        // O host vem do cabeçalho Host: só entra na política se tiver cara de host (nada de espaço ou ponto e vírgula, que
        // abririam espaço para injetar diretivas)
        var webSocket = IsSafeHost(host) ? " " + (isHttps ? "wss://" : "ws://") + host : string.Empty;
        var policy = "default-src 'self'; " +
                     "script-src 'self'; " +
                     "style-src 'self' 'unsafe-inline'; " +
                     "style-src-elem 'self'; " +
                     "style-src-attr 'unsafe-inline'; " +
                     "img-src 'self' data: https:; " +
                     "font-src 'self'; " +
                     $"connect-src 'self'{webSocket}; " +
                     "object-src 'none'; " +
                     "base-uri 'self'; " +
                     "form-action 'self'; " +
                     "frame-ancestors 'none'";

        // Só atrás de HTTPS: em http://localhost isso tentaria trocar todo recurso para https e quebraria o ambiente local
        return isHttps ? policy + "; upgrade-insecure-requests" : policy;
    }

    private static bool IsSafeHost(string? host) =>
        !string.IsNullOrEmpty(host) && host.Length <= 255
        && host.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or ':' or '[' or ']');
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
