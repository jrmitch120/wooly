namespace Wooly.Core.Posts;

/// <summary>One answer on a <see cref="PostPoll" />, as it stands rather than as it was composed.</summary>
public sealed record PostPollOption
{
    /// <summary>The answer's own text.</summary>
    public required string Text { get; init; }

    /// <summary>
    ///     How many votes this answer has drawn, or <see langword="null" /> when the instance withholds the breakdown
    ///     until this profile votes or the poll closes — a real third state, distinct from a genuine zero.
    /// </summary>
    public long? Votes { get; init; }

    /// <summary>Whether the profile reading the post chose this answer.</summary>
    public required bool Picked { get; init; }

    /// <summary>
    ///     The custom emoji the poll lists, which its answers' shortcodes are drawn from: the poll's own list rather
    ///     than the post's, and the same on every answer, since the wire keeps no list per answer (ADR-0035). The
    ///     shortcodes stay in <see cref="Text" /> as written.
    /// </summary>
    public IReadOnlyList<CustomEmoji> CustomEmoji { get; init; } = [];
}
