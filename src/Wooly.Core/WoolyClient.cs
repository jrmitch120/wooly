namespace Wooly.Core;

/// <summary>Fixed identifiers for this client, shared by the core layer and both front ends.</summary>
public static class WoolyClient
{
    /// <summary>
    ///     The name this client is invoked and identified by. Single source of truth for the CLI's help text, the TUI's
    ///     title bar, and the version banner.
    /// </summary>
    public const string Name = "mastodon-cli";

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
