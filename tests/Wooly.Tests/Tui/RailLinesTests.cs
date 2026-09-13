using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The rail's one mark column: <c>▶</c> where the tabbing has got to, <c>▷</c> where the selection has settled
///     only while the two differ, blank otherwise — collapsed from two columns to one so a reader is not shown
///     <c>▶▸</c> side by side for the whole ~250ms the two coincide (#78, ADR-0014).
/// </summary>
public class RailLinesTests
{
    private static Rail ARail(int destinations = 3, string label = "dest")
    {
        var list = Enumerable.Range(0, destinations)
            .Select(at => new Destination((DestinationKind)at, $"{label}{at}"))
            .ToList();

        return new Rail(list, new FakeShellHost(), TimeSpan.FromMilliseconds(250));
    }

    /// <summary>At rest the cursor and the selection are the same row, so only the filled mark shows there.</summary>
    [Fact]
    public void Of_MarksOnlyTheCursorsRowWhenCursorAndCurrentAgree()
    {
        var rail = ARail();

        var lines = RailLines.Of(rail, null, height: 10);

        Assert.StartsWith("▶ ", lines[0].Text);
        Assert.StartsWith("  ", lines[1].Text);
        Assert.StartsWith("  ", lines[2].Text);
    }

    /// <summary>
    ///     Mid-walk, before the settle window closes, the cursor has moved on but the selection has not caught up —
    ///     the cursor's row takes the filled mark, the selection's the hollow one, and nothing else.
    /// </summary>
    [Fact]
    public void Of_MarksTheCursorFilledAndTheSettledRowHollowWhileTheyDiffer()
    {
        var rail = ARail();

        rail.Step(2);

        var lines = RailLines.Of(rail, null, height: 10);

        Assert.StartsWith("▷ ", lines[0].Text);
        Assert.StartsWith("  ", lines[1].Text);
        Assert.StartsWith("▶ ", lines[2].Text);
    }

    /// <summary>
    ///     The freed second column goes to the destination label: one mark column and a space leave sixteen for the
    ///     label, where the old two-mark layout left only fifteen.
    /// </summary>
    [Fact]
    public void Of_GivesTheColumnTheSecondMarkUsedToHoldToTheLabel()
    {
        var label = new string('x', RailLines.Width - 2);
        var rail = new Rail([new Destination(DestinationKind.Home, label)], new FakeShellHost(), TimeSpan.FromMilliseconds(250));

        var lines = RailLines.Of(rail, null, height: 10);

        Assert.Equal($"▶ {label}", lines[0].Text);
    }

    /// <summary>
    ///     A rail entry is as many columns wide as the rail, whatever its label is written in. A hashtag rail entry
    ///     takes the tag's own name, and one written in a two-column script padded out by its characters would be
    ///     drawn past the rail and into the content beside it (#207).
    /// </summary>
    [Theory]
    [InlineData("#ドット絵")]
    [InlineData("#ドット絵のアカウントですどうぞ")]
    [InlineData("#photography")]
    public void Of_PadsARailEntryToTheRailsColumnsWhateverItsLabelIsWrittenIn(string label)
    {
        var rail = new Rail(
            [new Destination(DestinationKind.Hashtag, label)],
            new FakeShellHost(),
            TimeSpan.FromMilliseconds(250));

        var lines = RailLines.Of(rail, null, height: 10);

        Assert.Equal(RailLines.Width, lines[0].Width);
    }

    /// <summary>
    ///     The rail's two rules fall either side of the group Discover joined: the four timelines above the first,
    ///     the five you-go-to-them destinations between, and the profile's own account below the second (#181).
    /// </summary>
    /// <remarks>
    ///     Asserted as where the rules land rather than as the indices the code holds, because what a reader sees is
    ///     the grouping: a tenth entry that pushed the second rule the wrong way would put Discover in with the
    ///     profile, which is the one thing this ticket must not change.
    /// </remarks>
    [Fact]
    public void Of_KeepsTheGroupsEitherSideOfTheTenthDestination()
    {
        var rail = new Rail(
            [
                new Destination(DestinationKind.Home, "Home"),
                new Destination(DestinationKind.Local, "Local"),
                new Destination(DestinationKind.Federated, "Federated"),
                new Destination(DestinationKind.Hashtag, "Hashtag"),
                new Destination(DestinationKind.Notifications, "Notifications"),
                new Destination(DestinationKind.Messages, "Direct messages"),
                new Destination(DestinationKind.Requests, "Follow requests"),
                new Destination(DestinationKind.Search, "Search"),
                new Destination(DestinationKind.Discover, "Discover"),
                new Destination(DestinationKind.Profile, "@jeff"),
            ],
            new FakeShellHost(),
            TimeSpan.FromMilliseconds(250));

        var drawn = RailLines.Of(rail, null, height: 20).Select(line => line.Text.Trim()).ToList();

        var rule = new string('\u2500', RailLines.Width);

        // Four timelines, a rule, the five you go to — Search then Discover — a rule, and the profile below it.
        Assert.Equal(rule, drawn[4]);
        Assert.Equal("Search", drawn[8]);
        Assert.Equal("Discover", drawn[9]);
        Assert.Equal(rule, drawn[10]);
        Assert.Equal("@jeff", drawn[11]);
    }
}
