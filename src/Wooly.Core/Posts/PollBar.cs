namespace Wooly.Core.Posts;

/// <summary>
///     What one poll option's row says, worked out in the one place both the CLI's post report and the TUI read it
///     from — the percentage share of the vote an option drew, the block bar filled to it, and the row the two go on
///     — so the two surfaces cannot come to disagree about the same option's own numbers.
/// </summary>
/// <remarks>
///     The row is here for the reason <see cref="PostMedia.Shows" /> and <see cref="LinkPreview.Says" /> are: what a
///     post says is the same fact whichever front end is saying it, and a rule written twice is a rule that comes to
///     disagree with itself — which this one had, one surface grouping a thousand votes and the other not (#150).
///     Only the formula is here. The mark, the indent, the role and the width are each surface's own.
/// </remarks>
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

    /// <summary>
    ///     One option's whole row: a <c>✓ </c> where this profile picked the option and two blanks where it did not,
    ///     the bar, the share of the vote it drew, the count as a reader reads it, and the option's own text two
    ///     columns clear of it. An option whose count is withheld — real until this profile votes or the poll closes,
    ///     not the same thing as a genuine zero — is its lead and its text alone, no bar at all rather than one
    ///     guessed at.
    /// </summary>
    public static string RowOf(PostPoll poll, PostPollOption option) =>
        RowOf(poll, option, option.Picked ? "✓ " : "  ");

    /// <inheritdoc cref="RowOf(PostPoll, PostPollOption)" />
    /// <param name="mark">
    ///     What the row leads with, for a surface with something else to lead it with: the TUI passes a ballot's
    ///     <c>[x] </c>/<c>[ ] </c> while a vote is toggled and uncast, which the CLI has no equivalent of and never
    ///     needs. Everywhere else the overload above is the one to call — what this profile picked reads the same on
    ///     both surfaces, so it is written here rather than at each of them.
    /// </param>
    public static string RowOf(PostPoll poll, PostPollOption option, string mark)
    {
        if (option.Votes is not { } votes)
        {
            return $"{mark}{option.Text}";
        }

        var percent = PercentOf(poll, votes);

        return $"{mark}{Of(percent)} {percent}% ({Number.Of(votes)})  {option.Text}";
    }
}
