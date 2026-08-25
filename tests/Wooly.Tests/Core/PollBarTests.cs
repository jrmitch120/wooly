using System.Globalization;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Core;

/// <summary>
///     What one poll option's row says, asserted here rather than once per surface: the row is the CLI's and the
///     TUI's own the way <see cref="PostMedia.Shows" /> is, so the point of it living on <see cref="PollBar" /> is
///     that the two cannot come to disagree about the same option's numbers (#150).
/// </summary>
public class PollBarTests
{
    /// <summary>
    ///     The row a counted option reads back as: a <c>✓</c> where this profile picked it, a bar sized to the share
    ///     of the vote it drew, that share, the count in brackets, and the option's own text two columns clear of it.
    ///     What this profile picked reads the same on both surfaces, so neither is asked what a tick looks like.
    /// </summary>
    [Fact]
    public void RowOf_ReadsATickTheBarTheShareTheCountAndTheText()
    {
        var poll = APost.APoll(
            options: [APost.AnAnswer("Cats", 4), APost.AnAnswer("Dogs", 6, picked: true)],
            votes: 10);

        Assert.Equal("  ▓▓▓▓░░░░░░ 40% (4)  Cats", PollBar.RowOf(poll, poll.Options[0]));
        Assert.Equal("✓ ▓▓▓▓▓▓░░░░ 60% (6)  Dogs", PollBar.RowOf(poll, poll.Options[1]));
    }

    /// <summary>
    ///     A surface with something else to lead the row with says so, and the rest of the row is unchanged by it —
    ///     the TUI's ballot, which stands in for the tick rather than sitting beside it (#87).
    /// </summary>
    [Fact]
    public void RowOf_LeadsWithWhateverTheSurfaceGaveItInsteadOfTheTick()
    {
        var poll = APost.APoll(options: [APost.AnAnswer("Dogs", 6, picked: true)], votes: 10);

        Assert.Equal("[x] ▓▓▓▓▓▓░░░░ 60% (6)  Dogs", PollBar.RowOf(poll, poll.Options[0], "[x] "));
    }

    /// <summary>
    ///     An instance withholds a per-option breakdown until this profile votes or the poll closes — a third state,
    ///     distinct from a genuine zero, that draws no bar at all rather than guess at one. Asserted once here rather
    ///     than once per surface, since neither surface decides it any more (#150).
    /// </summary>
    [Fact]
    public void RowOf_DrawsNoBarAtAllForAnOptionWhoseCountIsWithheld()
    {
        var poll = APost.APoll(options: [APost.AnAnswer("Cats", null)], votes: 0);

        Assert.Equal("  Cats", PollBar.RowOf(poll, poll.Options[0]));
    }

    /// <summary>A genuinely unvoted option still draws a bar, at 0% — not the same thing as a withheld count.</summary>
    [Fact]
    public void RowOf_DrawsAnEmptyBarAndZeroPercentForAGenuinelyUnvotedOption()
    {
        var poll = APost.APoll(options: [APost.AnAnswer("Cats", 0), APost.AnAnswer("Dogs", 6)], votes: 6);

        Assert.Equal("  ░░░░░░░░░░ 0% (0)  Cats", PollBar.RowOf(poll, poll.Options[0]));
    }

    /// <summary>
    ///     The count is grouped the way the reader's own machine groups numbers, on both surfaces: a thousand votes
    ///     is a number a person reads, and reading it is no more a screen's business than a pipe's (#150).
    /// </summary>
    [CulturedFact(["en-US", "de-DE"])]
    public void RowOf_GroupsTheCountTheWayTheReadersOwnMachineDoes()
    {
        var poll = APost.APoll(options: [APost.AnAnswer("Cats", 4210)], votes: 10000);
        var expected = CultureInfo.CurrentCulture.Name == "en-US" ? "4,210" : "4.210";

        Assert.Contains($"({expected})", PollBar.RowOf(poll, poll.Options[0]), StringComparison.Ordinal);
    }
}
