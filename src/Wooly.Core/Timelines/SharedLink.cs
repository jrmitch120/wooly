namespace Wooly.Core.Timelines;

/// <summary>
///     The rule for which addresses have a link timeline to read (#603), in the one place every entry point can reach
///     it, as <see cref="Hashtag" /> is for a tag: the CLI turning a typed address down as a usage error and the domain
///     refusing one that got past it ask the same question, so neither can start saying something the other does not.
///     <para>
///         A web page addressed in full — <c>http</c> or <c>https</c>, and a host. An instance finds a
///         <b>Trending link</b> by the exact address it holds for it, so this is stricter than what a browser is
///         handed (<see cref="BrowserLaunch.Address" />): a page named without its scheme is not given one here, since
///         a guessed scheme that missed would come back as a link that is not trending, which is a different answer
///         to a different mistake.
///     </para>
/// </summary>
public static class SharedLink
{
    /// <summary>
    ///     The address <paramref name="written" /> names, or <see langword="null" /> where it names no web page in full.
    ///     Kept as written (<see cref="Uri.OriginalString" />), surrounding whitespace aside, because that is what the
    ///     instance is asked for.
    /// </summary>
    public static Uri? Parse(string? written) =>
        written is not null
        && Uri.TryCreate(written.Trim(), UriKind.Absolute, out var address)
        && IsReadable(address)
            ? address
            : null;

    /// <summary>Whether <paramref name="address" /> is one whose sharing posts this client can ask an instance for.</summary>
    public static bool IsReadable(Uri address) =>
        address.IsAbsoluteUri
        && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps)
        && address.Host.Length > 0;

    /// <summary>How a value that is not such an address is described, wherever it is turned down.</summary>
    public static string Rejection(string written) =>
        $"Give the link as a whole web address — https://example.com/article, not '{written}'.";
}
