using System.Net;
using System.Net.Sockets;

namespace Monburan.Web.Services;

/// <summary>
/// Diz se um endereço IP é público. Usado na conexão com provedores de IA para o servidor não ser levado a acessar a
/// rede interna (SSRF): loopback, redes privadas, link-local (inclui o endereço de metadados da nuvem, 169.254.169.254),
/// CGNAT, reservados e documentação são tratados como não públicos.
/// </summary>
public static class IpAddressClassifier
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return false;

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicIPv4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicIPv6(address),
            _ => false
        };
    }

    private static bool IsPublicIPv4(byte[] b) => !(
        b[0] == 0                                      // 0.0.0.0/8 (esta rede)
        || b[0] == 10                                  // 10.0.0.0/8
        || (b[0] == 100 && b[1] is >= 64 and <= 127)   // 100.64.0.0/10 (CGNAT)
        || b[0] == 127                                 // 127.0.0.0/8
        || (b[0] == 169 && b[1] == 254)                // 169.254.0.0/16 (link-local e metadados da nuvem)
        || (b[0] == 172 && b[1] is >= 16 and <= 31)    // 172.16.0.0/12
        || (b[0] == 192 && b[1] == 0 && b[2] == 0)     // 192.0.0.0/24
        || (b[0] == 192 && b[1] == 0 && b[2] == 2)     // 192.0.2.0/24 (documentação)
        || (b[0] == 192 && b[1] == 168)                // 192.168.0.0/16
        || (b[0] == 198 && b[1] is 18 or 19)           // 198.18.0.0/15 (testes de desempenho)
        || (b[0] == 198 && b[1] == 51 && b[2] == 100)  // 198.51.100.0/24 (documentação)
        || (b[0] == 203 && b[1] == 0 && b[2] == 113)   // 203.0.113.0/24 (documentação)
        || b[0] >= 224);                               // multicast, reservado e broadcast

    private static bool IsPublicIPv6(IPAddress address)
    {
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal)
            return false;

        var b = address.GetAddressBytes();

        // ::/96: "::" (não especificado) e os IPv4 embutidos no formato antigo (::a.b.c.d)
        if (b.Take(12).All(value => value == 0))
            return false;

        // 100::/64 (descarte): não é roteável
        if (b[0] == 0x01 && b[1] == 0 && b.Skip(2).Take(6).All(value => value == 0))
            return false;

        // 2001:db8::/32 (documentação), 2001::/32 (Teredo) e 2002::/16 (6to4) podem embutir IPv4 interno
        if (b[0] == 0x20 && ((b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) || (b[1] == 0x01 && b[2] == 0 && b[3] == 0) || b[1] == 0x02))
            return false;

        // 64:ff9b::/96 (NAT64) traduz para IPv4 arbitrário
        return !(b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b);
    }
}
