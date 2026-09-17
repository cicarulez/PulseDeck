using System.Net;
using System.Net.Sockets;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public static class NewsHttp
{
    public static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false, UseCookies = false, UseProxy = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectCallback = async (context, token) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            // Connect to the validated address itself; no second DNS lookup or local-network access.
            foreach (var address in addresses.Where(NewsUrls.IsPublic))
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), token); return new NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); if (token.IsCancellationRequested) throw; }
            }
            throw new HttpRequestException("Public news endpoint unavailable.");
        }
    };
}
