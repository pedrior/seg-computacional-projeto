using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Guardian.Proxy;

public sealed partial class ProxyServer : IDisposable
{
    public const string HttpEndOfHeader = "\r\n\r\n";
    public const string HttpsConnectMethod = "CONNECT";

    private static readonly string[] KnownHttpMethods =
    [
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "HEAD",
        "OPTIONS",
        "PATCH"
    ];

    private const int BufferSize = 512;
    private const int SendTimeoutMs = 30_000;
    private const int ReceiveTimeoutMs = 30_000;

    private readonly TcpListener listener;
    private readonly CancellationTokenSource cts = new();

    private bool disposed;

    public ProxyServer(int port)
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Server.NoDelay = true;
        listener.Server.SetSocketOption(
            SocketOptionLevel.Socket,
            SocketOptionName.ReuseAddress,
            true);
    }

    public Func<string, bool>? Filter { get; private set; }

    public async Task StartAsync()
    {
        try
        {
            listener.Start();

            while (!cts.IsCancellationRequested)
            {
                // Accepts incoming connection requests
                var client = await listener.AcceptTcpClientAsync()
                    .ConfigureAwait(false);

                // Handle client request asynchronously in a fire-and-forget manner
                _ = HandleClientAsync(client, cts.Token);
            }
        }
        catch (ObjectDisposedException)
        {
            // Listener was stopped
        }
        catch (SocketException exception)
        {
            Debug.WriteLine($"{exception.SocketErrorCode}: {exception.Message}");
        }
    }

    public void Stop()
    {
        if (cts.IsCancellationRequested)
        {
            // Already requested
            return;
        }

        cts.Cancel();
        listener.Stop();
    }

    public void UseFilter(Func<string, bool> filter) => Filter = filter;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        // Stop the server if it's still running
        Stop();

        cts.Dispose();

        disposed = true;
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellation)
    {
        try
        {
            using (client) // Dispose the client when the method completes
            await using (var stream = client.GetStream())
            {
                var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

                try
                {
                    // Read the request line
                    var bytesRead = await stream.ReadAsync(buffer, cancellation)
                        .ConfigureAwait(false);

                    if (!IsEitherHttpOrHttps(buffer, bytesRead))
                    {
                        return; // Not a valid HTTP or HTTPS request
                    }

                    // Parse the request line to get the method, URL, and HTTP version
                    var (method, url, version) = ParseRequestLine(buffer, bytesRead);

                    if (Filter is not null && !Filter(url))
                    {
                        return; // URL is filtered
                    }

                    if (IsHttps(method))
                    {
                        await HandleHttpsRequestAsync(stream, url, cancellation)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await HandleHttpRequestAsync(stream, buffer, bytesRead, method, url, version, cancellation)
                            .ConfigureAwait(false);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Client handling error: {ex.Message}");
        }
    }

    private static bool IsEitherHttpOrHttps(byte[] buffer, int bytesRead)
    {
        const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
        var data = Encoding.ASCII.GetString(buffer, index: 0, bytesRead);

        // Check if it's HTTP (starts with "GET", "POST", etc.) or HTTPS (starts with "CONNECT")
        return KnownHttpMethods.Any(method => data.StartsWith(method, comparison)) ||
               data.StartsWith(HttpsConnectMethod, comparison);
    }

    private static (string method, string url, string version) ParseRequestLine(byte[] buffer, int bytesRead)
    {
        var segments = GetRequestLine(buffer, bytesRead)
            .Split(' ');

        return segments.Length < 3
            ? throw new InvalidOperationException("Invalid request line")
            : (segments[0], segments[1], segments[2]);
    }

    private static string GetRequestLine(byte[] buffer, int bytesRead)
    {
        const string crlf = "\r\n";

        var data = Encoding.ASCII.GetString(buffer, index: 0, bytesRead);
        var eol = data.IndexOf(crlf, StringComparison.Ordinal);

        // Return the request line without the trailing CRLF
        return eol >= 0 ? data[..eol] : data;
    }

    private static bool IsHttps(string method) =>
        method.Equals(HttpsConnectMethod, StringComparison.OrdinalIgnoreCase);
}