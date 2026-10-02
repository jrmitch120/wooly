using Wooly.Core.Http;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

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
    ///     A rule falls between each rail group and the next, wherever the groups happen to change: the timelines, a
    ///     rule, Discover and Search, a rule, the Inbox, a rule, and the profile's own account (ADR-0021, #264).
    /// </summary>
    /// <remarks>
    ///     Asserted as where the rules land rather than as the indices the code once held, because what a reader sees
    ///     is the grouping, and a reorder that left a rule behind would split a group in two.
    /// </remarks>
    [Fact]
    public void Of_DrawsARuleBetweenEachGroupAndTheNext()
    {
        var rail = new Rail(
            [
                new Destination(DestinationKind.Home, "Home"),
                new Destination(DestinationKind.Local, "Local"),
                new Destination(DestinationKind.Federated, "Federated"),
                new Destination(DestinationKind.Hashtag, "Hashtag"),
                new Destination(DestinationKind.Discover, "Discover"),
                new Destination(DestinationKind.Search, "Search"),
                new Destination(DestinationKind.Notifications, "Notifications"),
                new Destination(DestinationKind.Messages, "Direct messages"),
                new Destination(DestinationKind.Requests, "Follow requests"),
                new Destination(DestinationKind.Profile, "@jeff"),
            ],
            new FakeShellHost(),
            TimeSpan.FromMilliseconds(250));

        var rule = new string('\u2500', RailLines.Width);

        var drawn = RailLines.Of(rail, null, height: 20).Take(13).Select(line => line.Text.Trim());

        Assert.Equal(
            [
                "Home", "Local", "Federated", "Hashtag", rule,
                "Discover", "Search", rule,
                "Notifications", "Direct messages", "Follow requests", rule,
                "@jeff",
            ],
            drawn.Select(text => text.TrimStart('▶', ' ')));
    }

    /// <summary>Destinations of one group are drawn with nothing between them, however many there are.</summary>
    [Fact]
    public void Of_DrawsNoRuleWithinAGroup()
    {
        var rail = new Rail(
            [
                new Destination(DestinationKind.Notifications, "Notifications"),
                new Destination(DestinationKind.Messages, "Direct messages"),
                new Destination(DestinationKind.Requests, "Follow requests"),
            ],
            new FakeShellHost(),
            TimeSpan.FromMilliseconds(250));

        var drawn = RailLines.Of(rail, null, height: 10).Take(4).Select(line => line.Text.Trim());

        Assert.Equal(
            ["Notifications", "Direct messages", "Follow requests", string.Empty],
            drawn.Select(text => text.TrimStart('▶', ' ')));
    }

    /// <summary>
    ///     With one profile there is nothing to tell apart, so the foot is the rule and the quota and nothing between
    ///     them — the rail row for row as it was before the instance had a row to go on (#241).
    /// </summary>
    [Fact]
    public void Of_DrawsNoInstanceRowWithoutAnInstance()
    {
        var quota = new RateLimitQuota(250, 300, null);

        var drawn = RailLines.Of(ARail(), quota, height: 10, instance: null);

        Assert.Equal(10, drawn.Count);
        Assert.Equal(new string('\u2500', RailLines.Width), drawn[^2].Text);
        Assert.Equal($" {RailLines.Spent(quota)}", drawn[^1].Text.TrimEnd());
        Assert.All(drawn.Take(drawn.Count - 2).Skip(3), line => Assert.Equal(new string(' ', RailLines.Width), line.Text));
    }

    /// <summary>
    ///     With two or more profiles, the instance sits on its own row directly above the quota, in the quota's own
    ///     role — a fact about the frame, not a destination — and the destinations keep the rows they had (#241).
    /// </summary>
    [Fact]
    public void Of_PutsTheInstanceOnItsOwnRowDirectlyAboveTheQuota()
    {
        var quota = new RateLimitQuota(250, 300, null);
        var rail = ARail();

        var without = RailLines.Of(rail, quota, height: 10);
        var drawn = RailLines.Of(rail, quota, height: 10, instance: "hachyderm.io");

        Assert.Equal(10, drawn.Count);
        Assert.Equal(" hachyderm.io".PadRight(RailLines.Width), drawn[^2].Text);
        Assert.Equal(Role.Quota, drawn[^2].Role);
        Assert.Equal(new string('\u2500', RailLines.Width), drawn[^3].Text);
        Assert.Equal(without[^1].Text, drawn[^1].Text);
        Assert.Equal(without.Take(3).Select(line => line.Text), drawn.Take(3).Select(line => line.Text));
    }

    /// <summary>
    ///     A long instance is clipped to the rail by the wrapping every rail row is cut by, losing its end: the row is
    ///     the rail's width and never past it into the content (#241).
    /// </summary>
    [Fact]
    public void Of_ClipsALongInstanceToTheRail()
    {
        var drawn = RailLines.Of(ARail(), null, height: 10, instance: "social.a-very-long-instance.example");

        Assert.Equal(RailLines.Width, drawn[^2].Width);
        Assert.StartsWith(" social.", drawn[^2].Text);
        Assert.EndsWith("…", drawn[^2].Text);
    }
}
