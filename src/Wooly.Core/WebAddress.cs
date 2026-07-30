namespace Wooly.Core;

/// <summary>
///     A URL written down as text for something outside this process — an OS asked to open it, or a user asked to copy
///     it out of a terminal.
/// </summary>
public static class WebAddress
{
    /// <summary>
    ///     The address as text that parses back into the URL it came from.
    /// </summary>
    /// <remarks>
    ///     <see cref="Uri.AbsoluteUri" /> rather than <see cref="Uri.ToString" />, which is a display form: it
    ///     unescapes whatever did not strictly need escaping, so <c>scope=read%20write</c> leaves here as
    ///     <c>scope=read write</c> and is no longer a URL anyone can parse. Whatever receives such a string has to make
    ///     a URL of it again, and a percent-escaping pass over the whole of it escapes the escapes already in it — which
    ///     is how an authorization request reaches an instance with its <c>redirect_uri</c> encoded twice, against a
    ///     redirect URI the instance registered encoded once.
    /// </remarks>
    public static string Of(Uri url) => url.AbsoluteUri;
}
