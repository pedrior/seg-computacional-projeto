using System.Net.Sockets;
using System.Text;

namespace Guardian.Proxy;

public sealed partial class ProxyServer
{
    private static async Task HandleHttpsRequestAsync(
        NetworkStream clientStream,
        string url,
        CancellationToken cancellation)
    {
        // Unpack the host and port from the URL
        var (host, port) = ParseHostAndPort(url, 443);
        using var targetClient = new TcpClient(host, port);

        targetClient.SendTimeout = SendTimeoutMs;
        targetClient.ReceiveTimeout = ReceiveTimeoutMs;

        await using var stream = targetClient.GetStream();

        // Send a 200 Connection Established response to the client
        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 Connection Established{HttpEndOfHeader}");
        await clientStream.WriteAsync(response, cancellation)
            .ConfigureAwait(false);

        // Handle bidirectional data transfer between the client and the target server.
        await Task.WhenAny(
                clientStream.CopyToAsync(stream, cancellation),
                stream.CopyToAsync(clientStream, cancellation))
            .ConfigureAwait(false);
    }

    private static (string host, int port) ParseHostAndPort(string url, int defaultPort)
    {
        var segments = url.Split(':');
        return segments.Length switch
        {
            1 => (segments[0], defaultPort),
            2 => (segments[0], int.Parse(segments[1])),
            _ => throw new FormatException("Invalid host/port format")
        };
    }
}