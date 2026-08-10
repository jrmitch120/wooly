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
}
