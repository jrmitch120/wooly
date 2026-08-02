using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     Where a scrolling region starts drawing, so that what the reader picked out is on screen. Worth a test of its
///     own because its own answer is its next input: one that disagreed with itself would flip between two positions
///     for as long as anything was redrawing, which is what a post carrying more pictures than fit on a screen showed
///     as very fast flicker (ADR-0016).
/// </summary>
public class ScrollTests
{
    /// <summary>
    ///     The property everything else rests on: asked twice over the same rows, the answer is the same twice. Checked
    ///     across every shape that matters — a selection above, below, inside and larger than the room.
    /// </summary>
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(0, 1, 40)]
    [InlineData(30, 1, 0)]
    [InlineData(90, 1, 0)]
    [InlineData(0, 80, 0)]
    [InlineData(0, 80, 40)]
    [InlineData(10, 60, 0)]
    [InlineData(10, 60, 90)]
    [InlineData(0, 200, 0)]
    public void To_SettlesRatherThanFlippingBetweenTwoPositions(int at, int tall, int from)
    {
        var lines = Rows(100, at, tall);

        var once = Scroll.To(lines, 20, from);
        var twice = Scroll.To(lines, 20, once);

        Assert.Equal(once, twice);
        Assert.Equal(once, Scroll.To(lines, 20, twice));
    }

    /// <summary>
    ///     A post taller than the terminal is shown from its top: the byline says whose post it is, and losing that is
    ///     worse than losing the last of the text. This is the case that used to flip.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(35)]
    [InlineData(99)]
    public void To_ShowsAPostTallerThanTheRoomFromItsTop(int from)
    {
        var lines = Rows(100, at: 10, tall: 60);

        Assert.Equal(10, Scroll.To(lines, 20, from));
    }

    /// <summary>A selection below what is on screen is scrolled to, and no further than it has to be.</summary>
    [Fact]
    public void To_ScrollsDownJustFarEnoughToShowASelectionBelow()
    {
        var lines = Rows(100, at: 40, tall: 3);

        // The last of it lands on the bottom row: 42 - 20 + 1.
        Assert.Equal(23, Scroll.To(lines, 20, from: 0));
    }

    /// <summary>A selection above what is on screen is scrolled back to, and it is its first row that is shown.</summary>
    [Fact]
    public void To_ScrollsUpToShowASelectionAbove()
    {
        var lines = Rows(100, at: 5, tall: 3);

        Assert.Equal(5, Scroll.To(lines, 20, from: 60));
    }

    /// <summary>A selection already on screen does not move the screen.</summary>
    [Fact]
    public void To_LeavesTheScreenAloneWhenTheSelectionIsAlreadyOnIt()
    {
        var lines = Rows(100, at: 35, tall: 3);

        Assert.Equal(30, Scroll.To(lines, 20, from: 30));
    }

    /// <summary>Nothing picked out is nothing to scroll to, and the screen stays where it was.</summary>
    [Fact]
    public void To_StaysWhereItIsWhenNothingIsPickedOut()
    {
        var lines = Rows(100, at: -1, tall: 0);

        Assert.Equal(30, Scroll.To(lines, 20, from: 30));
    }

    /// <summary>A region with no room in it has nowhere to scroll to.</summary>
    [Fact]
    public void To_AnswersTheTopForARegionWithNoRoom() =>
        Assert.Equal(0, Scroll.To(Rows(100, at: 40, tall: 3), height: 0, from: 10));

    /// <summary>
    ///     <paramref name="count" /> rows, of which <paramref name="tall" /> starting at <paramref name="at" /> carry
    ///     the selection — which is how a screen says which post the reader picked out.
    /// </summary>
    private static IReadOnlyList<Line> Rows(int count, int at, int tall) =>
        [
            .. Enumerable.Range(0, count)
                         .Select(row => row >= at && row < at + tall
                             ? Line.Of("▌", Role.Selection)
                             : Line.Of("text", Role.Body)),
        ];
}
