namespace Wooly.Core.Configuration;

/// <summary>Settings that change how this client behaves, rather than who it talks to.</summary>
public sealed record Preferences
{
    /// <summary>
    ///     The visibility a new post gets when the command line does not say, or <see langword="null" /> to leave the
    ///     choice to the instance's own default.
    /// </summary>
    public PostVisibility? DefaultVisibility { get; init; }
}
