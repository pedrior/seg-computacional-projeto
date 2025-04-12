using System.Net.Sockets;
using System.Text;

namespace Guardian.Proxy;

public sealed partial class ProxyServer
{
    private static async Task HandleHttpRequestAsync(
        NetworkStream clientStream,
        byte[] buffer,
        int bytesRead,
        string method,
        string url,
        string version,
        CancellationToken cancellation)
    {
        var uri = new Uri(url);

        // Connect to the target server and get the stream
        using var targetClient = new TcpClient(uri.Host, uri.Port);
        targetClient.SendTimeout = SendTimeoutMs;
        targetClient.ReceiveTimeout = ReceiveTimeoutMs;

        await using var targetStream = targetClient.GetStream();

        // Build and send the request to the target server
        var httpRequest = BuildHttpRequest(buffer, bytesRead, method, uri, version);
        await targetStream.WriteAsync(httpRequest, cancellation)
            .ConfigureAwait(false);

        // Handle bidirectional data transfer between the client and the target server
        await Task.WhenAny(
                clientStream.CopyToAsync(targetStream, cancellation),
                targetStream.CopyToAsync(clientStream, cancellation))
            .ConfigureAwait(false);
    }

    private static byte[] BuildHttpRequest(
        byte[] buffer,
        int bytesRead,
        string method,
        Uri uri,
        string version)
    {
        var originalRequest = Encoding.ASCII.GetString(buffer, index: 0, bytesRead);

        // Extract the headers from the request
        var eohIndex = originalRequest.IndexOf(HttpEndOfHeader, StringComparison.Ordinal);
        var headers = originalRequest[(originalRequest.IndexOf('\n') + 1)..eohIndex];

        // Build the new HTTP request with the modified URL
        var modifiedRequest = $"{method} {uri.PathAndQuery} {version}\r\n{headers}{HttpEndOfHeader}";

        // Extract the body
        var body = buffer.AsMemory(
            start: eohIndex + 4,
            length: bytesRead - (eohIndex + 4));

        // Combine the new request with the body
        var modifiedRequestBytes = Encoding.ASCII.GetBytes(modifiedRequest);
        var combined = new byte[modifiedRequestBytes.Length + body.Length];

        // Copy the modified request to the combined buffer
        Buffer.BlockCopy(
            src: modifiedRequestBytes,
            srcOffset: 0,
            dst: combined,
            dstOffset: 0,
            count: modifiedRequestBytes.Length);

        // Copy the body to the combined buffer
        body.CopyTo(combined.AsMemory(modifiedRequestBytes.Length));

        return combined;
    }
}