using System.Net;

using Microsoft.AspNetCore.HttpOverrides;

namespace Monburan.Web.Services;

/// <summary>
/// Atrás do nginx (o TLS termina nele) o app só enxerga HTTP vindo da rede interna. Com estes cabeçalhos ele passa a saber
/// o esquema (https) e o IP real da pessoa: o cookie de login sai com o atributo <c>Secure</c>, os redirecionamentos e
/// links usam https e os limites por IP contam por pessoa, não por proxy. Só se confia nos cabeçalhos de quem chega do
/// próprio computador ou de redes privadas (o nginx do host chega ao contêiner pela ponte do Docker); de um endereço público
/// qualquer cabeçalho <c>X-Forwarded-*</c> é ignorado, para ninguém forjar o IP nem o esquema.
/// </summary>
public static class ForwardedHeadersSetup
{
    public static void Apply(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Um proxy (o nginx) na frente do app: vale só o último valor de cada cabeçalho
        options.ForwardLimit = 1;

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var (address, prefix) in TrustedNetworks)
            options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse(address), prefix));
    }

    // Loopback, redes privadas (RFC 1918) e IPv6 local
    private static readonly (string Address, int Prefix)[] TrustedNetworks =
    [
        ("127.0.0.0", 8),
        ("::1", 128),
        ("10.0.0.0", 8),
        ("172.16.0.0", 12),
        ("192.168.0.0", 16),
        ("fc00::", 7)
    ];
}
