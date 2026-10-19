using System.Net;
using System.Net.Sockets;

namespace Monburan.Web.Services;

/// <summary>A conexão foi recusada porque o endereço do provedor resolve para uma rede interna ou reservada.</summary>
public sealed class AiBlockedDestinationException(string host)
    : IOException($"Destino bloqueado: {host} aponta para uma rede interna ou reservada.");

/// <summary>
/// Abre a conexão com o provedor de IA depois de resolver o DNS e conferir <b>todos</b> os endereços: assim o nome não
/// pode mudar de destino entre a checagem e a conexão (DNS rebinding) e um endereço público que também aponte para a
/// rede interna é recusado. Usado como <c>ConnectCallback</c> do <see cref="SocketsHttpHandler"/>.
/// </summary>
public static class AiNetworkGuard
{
    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context, bool allowPrivateNetworks, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

        if (addresses.Length == 0)
            throw new HttpRequestException($"Não foi possível resolver o endereço {host}.");

        if (!allowPrivateNetworks && addresses.Any(address => !IpAddressClassifier.IsPublic(address)))
            throw new AiBlockedDestinationException(host);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
