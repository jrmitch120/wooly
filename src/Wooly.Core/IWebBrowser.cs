namespace Wooly.Core;

/// <summary>
///     The user's web browser, as far as this client needs one. Two things send somebody to it: the part of an OAuth
///     sign-in that has to happen on the instance's own pages (ADR-0004), and an address a reader picked out inside a
///     post (#85). A seam as much as an abstraction — a test that opened the real browser would put a Mastodon login
///     page on whoever ran it.
/// </summary>
public interface IWebBrowser
{
    /// <summary>Tries to open <paramref name="url" /> in whatever browser this machine considers the user's.</summary>
    /// <returns>
    ///     Whether a browser was launched. Failing to launch one is an ordinary outcome, not an error: on a headless
    ///     machine there is nothing to launch, and the caller's answer to that is to show the address instead. An
    ///     address this client does not open at all (<see cref="BrowserLaunch" />) answers the same way, because one
    ///     bool cannot say two things — a caller that has to tell a reader which of the two happened asks
    ///     <see cref="BrowserLaunch.Address" /> before it asks this.
    /// </returns>
    bool TryOpen(Uri url);
}
