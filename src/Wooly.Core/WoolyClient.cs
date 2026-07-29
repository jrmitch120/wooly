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
    ///     The name the Mastodon-facing <see cref="HttpClient" /> is registered under with <c>AddHttpClient</c>. Tests
    ///     reconfigure this same name to swap in a fake primary handler.
    /// </summary>
    public const string HttpClientName = "wooly.mastodon";
}
