using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

using Monburan.Web.Endpoints;
using Monburan.Web.Services;

namespace Monburan.Web.Middleware;

/// <summary>
/// Limites de requisição por IP (o IP real vem do nginx; ver <see cref="ForwardedHeadersSetup"/>), em duas camadas:
/// um balde geral por IP para o site todo, para um IP só não esgotar o servidor para os outros; e um limite por minuto,
/// também por IP, nos envios de login, cadastro e recuperação de senha, que complementa o bloqueio da conta (o bloqueio
/// protege uma conta; este protege contra quem tenta muitas contas). <c>/health</c> não é limitado.
/// </summary>
public static class RateLimiting
{
    // Rotas sensíveis (só POST): páginas do Identity e API de autenticação
    private static readonly HashSet<string> SensitivePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/entrar",
        "/entrar/2fa",
        "/entrar/codigo-recuperacao",
        "/criar-conta",
        "/esqueci-senha",
        "/reenviar-confirmacao",
        "/redefinir-senha",
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/refresh",
        "/api/auth/forgot-password"
    };

    public static void Configure(RateLimiterOptions options, IOptionsMonitor<SecurityOptions> security)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = RejectAsync;
        options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(context => GeneralPartition(context, security.CurrentValue.RateLimit)),
            PartitionedRateLimiter.Create<HttpContext, string>(context => AuthPartition(context, security.CurrentValue.RateLimit)));
    }

    public static bool IsSensitive(string? path, string method) =>
        HttpMethods.IsPost(method) && path is not null && SensitivePaths.Contains(path.TrimEnd('/'));

    /// <summary>Quem conta como "o mesmo cliente": o IPv4, ou o bloco /64 no IPv6 (uma pessoa recebe um bloco inteiro).</summary>
    public static string ClientKey(IPAddress? address)
    {
        if (address is null)
            return "desconhecido";

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        var bytes = address.GetAddressBytes();
        return new IPAddress([.. bytes[..8], 0, 0, 0, 0, 0, 0, 0, 0]) + "/64";
    }

    private static RateLimitPartition<string> GeneralPartition(HttpContext context, SecurityOptions.RateLimitSettings settings)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
            return RateLimitPartition.GetNoLimiter("saude");

        var perSecond = Math.Max(1, settings.RequestsPerSecondPerIp);
        return RateLimitPartition.GetTokenBucketLimiter("geral:" + ClientKey(context.Connection.RemoteIpAddress), _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = perSecond * 2,
            TokensPerPeriod = perSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    private static RateLimitPartition<string> AuthPartition(HttpContext context, SecurityOptions.RateLimitSettings settings)
    {
        if (!IsSensitive(context.Request.Path.Value, context.Request.Method))
            return RateLimitPartition.GetNoLimiter("sem-limite-de-autenticacao");

        return RateLimitPartition.GetSlidingWindowLimiter("autenticacao:" + ClientKey(context.Connection.RemoteIpAddress), _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, settings.AuthRequestsPerMinutePerIp),
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    /// <summary>Método e caminho vêm do cliente: sem quebra de linha, ninguém forja uma linha de log; e com tamanho limitado.</summary>
    public static string ForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var cleaned = value.Replace("\r", string.Empty).Replace("\n", string.Empty);
        return cleaned.Length <= 200 ? cleaned : cleaned[..200];
    }

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay) ? delay : TimeSpan.FromSeconds(30);
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        http.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var logger = http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Monburan.RateLimit");
        logger?.LogWarning("Limite de requisições: {Metodo} {Rota} de {Cliente} recusado (tente de novo em {Segundos}s)",
            ForLog(http.Request.Method), ForLog(http.Request.Path.Value), ClientKey(http.Connection.RemoteIpAddress), seconds);

        const string message = "Muitas tentativas em pouco tempo. Aguarde um instante e tente de novo.";
        if (http.Request.Path.StartsWithSegments("/api"))
        {
            await ApiProblemDetails.WriteAsync(http, StatusCodes.Status429TooManyRequests, message);
            return;
        }

        http.Response.ContentType = "text/plain; charset=utf-8";
        await http.Response.WriteAsync(message, cancellationToken);
    }
}
