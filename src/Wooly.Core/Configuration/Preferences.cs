using Wooly.Core.Posts;

namespace Wooly.Core.Configuration;

/// <summary>Settings that change how this client behaves, rather than who it talks to.</summary>
public sealed record Preferences
{
    /// <summary>
    ///     The visibility a new post gets when the command line does not say, or <see langword="null" /> to leave the
    ///     choice to the instance's own default.
    /// </summary>
    public PostVisibility? DefaultVisibility { get; init; }

    /// <summary>
    ///     The hashtag the TUI's rail keeps a destination for, without its leading <c>#</c>, or <see langword="null" />
    ///     where the reader has not named one yet. A setting rather than a fixed tag because the four timelines are the
    ///     same four for everybody and a tag worth a permanent place on the rail is not.
    /// </summary>
    public string? Hashtag { get; init; }
}
