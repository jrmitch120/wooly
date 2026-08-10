namespace Wooly.Core.Posts;

/// <summary>
///     What one poll option's bar says, worked out in the one place both the CLI's post report and the TUI compute
///     it from — the percentage share of the vote an option drew, and the block bar filled to it — so the two
///     cannot come to disagree about the same option's own numbers.
/// </summary>
public static class PollBar
{
    /// <summary>How many cells the bar spends, filled or not.</summary>
    public const int Width = 10;

    /// <summary><paramref name="votes" />'s share of <paramref name="poll" />'s total, rounded to the nearest percent.</summary>
    /// <remarks>0% for a poll nobody has voted in yet, rather than the division by zero a genuine share would be.</remarks>
    public static int PercentOf(PostPoll poll, long votes) =>
        poll.Votes == 0 ? 0 : (int)Math.Round(votes * 100.0 / poll.Votes);

    /// <summary>The bar itself: <paramref name="percent" /> out of <see cref="Width" /> cells filled.</summary>
    public static string Of(int percent)
    {
        var filled = Math.Clamp(percent / 10, 0, Width);

        return new string('▓', filled) + new string('░', Width - filled);
    }
}
