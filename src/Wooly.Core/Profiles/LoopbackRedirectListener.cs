using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace Wooly.Core.Profiles;

/// <summary>
///     The far end of an OAuth redirect: a socket on this machine's loopback interface that the instance sends the
///     user's browser to once they have answered the authorization page. A terminal client has nowhere else to be
///     redirected — it has no web address of its own — so it borrows one for the length of a sign-in.
/// </summary>
/// <remarks>
///     This speaks just enough HTTP to be redirected to. A full <see cref="HttpListener" /> would want a URL
///     reservation from the OS on Windows, which is an administrator's job and no way to start a sign-in; a socket
///     bound to loopback needs nobody's permission on any of the three platforms.
/// </remarks>
public sealed class LoopbackRedirectListener : IDisposable
{
    private readonly TcpListener _listener;

    private LoopbackRedirectListener(TcpListener listener, Uri redirectUri)
    {
        _listener = listener;
        RedirectUri = redirectUri;
    }

    /// <summary>The address to hand the instance as this authorization request's redirect URI.</summary>
    public Uri RedirectUri { get; }

    /// <summary>
    ///     Binds a port and starts listening. The port is whichever one the OS has free, asked for as port 0, because a
    ///     fixed one would collide with anything else on the machine — including a second copy of this client midway
    ///     through its own sign-in.
    /// </summary>
    public static LoopbackRedirectListener Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        // The literal address rather than "localhost": that name can resolve to IPv6 first, and this is bound to the
        // IPv4 loopback only, so a browser following it could arrive nowhere.
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        return new LoopbackRedirectListener(listener, new Uri($"http://127.0.0.1:{port}/"));
    }

    /// <summary>
    ///     Waits for the browser to be redirected here and reports what it brought. Requests that settle nothing are
    ///     answered and ignored, so the wait ends on an authorization rather than on a favicon.
    /// </summary>
    /// <param name="cancellationToken">
    ///     How the wait ends when the browser never comes back — nothing about this flow guarantees it does.
    /// </param>
    public async Task<AuthorizationRedirect> AwaitRedirect(CancellationToken cancellationToken)
    {
        while (true)
        {
            using var connection = await _listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();

            var redirect = await ReadRedirect(stream, cancellationToken);

            await WritePage(stream, redirect, cancellationToken);

            if (redirect.CarriesAnAuthorization)
            {
                return redirect;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();

    private async Task<AuthorizationRedirect> ReadRedirect(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

        // Only the request line is of any interest — everything the instance sent is in its query string. The headers
        // are still read to the blank line that ends them, so the browser is not answered mid-sentence.
        var requestLine = await reader.ReadLineAsync(cancellationToken);

        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
        {
        }

        var target = requestLine?.Split(' ') is [_, var path, ..] ? path : "/";
        var query = HttpUtility.ParseQueryString(new Uri(RedirectUri, target).Query);

        return new AuthorizationRedirect(
            query["code"],
            query["state"],
            query["error"],
            query["error_description"]);
    }

    private static async Task WritePage(
        NetworkStream stream,
        AuthorizationRedirect redirect,
        CancellationToken cancellationToken)
    {
        var (status, message) = redirect switch
        {
            { Code: not null } => ("200 OK", "You're connected. Close this tab and go back to your terminal."),
            { Error: not null } => ("200 OK", "That request was not authorized. Go back to your terminal for the details."),
            _ => ("404 Not Found", "Nothing is served here."),
        };

        // Written by hand rather than rendered, because this is the whole of this client's web presence: one sentence
        // the user reads before closing the tab.
        var body = Encoding.UTF8.GetBytes(
            $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>{WoolyClient.Name}</title></head>" +
            $"<body><p>{message}</p></body></html>");

        var head = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n");

        await stream.WriteAsync(head, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
