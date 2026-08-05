namespace Wooly.Core;

/// <summary>Fixed identifiers for this client, shared by the core layer and both front ends.</summary>
public static class WoolyClient
{
    /// <summary>
    ///     The one name this client is known by outside itself: what an instance registers and then shows on the page
    ///     listing the applications an account has approved (ADR-0004), and the namespace the OS keyring keeps tokens
    ///     under (ADR-0003).
    ///     <para>
    ///         Deliberately not the name either front end is invoked as. <c>wooly-cli</c> and <c>wooly-tui</c> are two
    ///         ways into one client, and were each to introduce itself as the command it was typed as, they would
    ///         authorize as two different applications and neither could read the tokens the other stored.
    ///     </para>
    /// </summary>
    public const string Name = "wooly";

    /// <summary>
    ///     Where a user who wants to know what this client is can go and read about it. Registered with every instance
    ///     a profile is authorized on (ADR-0004), which shows it beside this client's name on the page listing the
    ///     applications an account has approved.
    /// </summary>
    public const string Website = "https://github.com/jrmitch120/wooly";

    /// <summary>
    ///     The name the Mastodon-facing <see cref="HttpClient" /> is registered under with <c>AddHttpClient</c>. Tests
    ///     reconfigure this same name to swap in a fake primary handler.
    /// </summary>
    public const string HttpClientName = "wooly.mastodon";
}
