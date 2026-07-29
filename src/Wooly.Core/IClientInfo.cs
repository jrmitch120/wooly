namespace Wooly.Core;

/// <summary>
///     Identifies this client to users and to Mastodon instances. Both front ends read their version banner from here
///     so the CLI and the TUI can never disagree about what build is running.
/// </summary>
public interface IClientInfo
{
    /// <summary>The name the client is invoked and identified by.</summary>
    string Name { get; }

    /// <summary>The client's release version, e.g. <c>0.1.0</c>.</summary>
    string Version { get; }
}
