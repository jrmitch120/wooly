namespace Wooly.Core;

/// <summary>
///     The user's web browser, as far as this client needs one: somewhere to send them for the part of an OAuth
///     sign-in that has to happen on the instance's own pages (ADR-0004). A seam as much as an abstraction — a test
///     that opened the real browser would put a Mastodon login page on whoever ran it.
/// </summary>
public interface IWebBrowser
{
    /// <summary>Tries to open <paramref name="url" /> in whatever browser this machine considers the user's.</summary>
    /// <returns>
    ///     Whether a browser was launched. Failing to launch one is an ordinary outcome, not an error: on a headless
    ///     machine there is nothing to launch, and the caller's answer to that is to show the address instead.
    /// </returns>
    bool TryOpen(Uri url);
}
