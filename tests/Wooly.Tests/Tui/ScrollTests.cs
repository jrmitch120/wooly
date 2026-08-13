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

    /// <summary>
    ///     And stays where it is even at the foot of the rows, where the arrows can leave it: a keymap read to its end
    ///     and then walked with <c>j</c> must not be pulled back up a page by a scroll that has nothing to scroll to.
    /// </summary>
    [Fact]
    public void To_LeavesAScreenWithNothingPickedOutWhereTheArrowsPutIt()
    {
        var lines = Rows(100, at: -1, tall: 0);

        Assert.Equal(99, Scroll.To(lines, 20, from: Scroll.By(lines, from: 0, rows: 500)));
    }

    /// <summary>A region with no room in it has nowhere to scroll to.</summary>
    [Fact]
    public void To_AnswersTheTopForARegionWithNoRoom() =>
        Assert.Equal(0, Scroll.To(Rows(100, at: 40, tall: 3), height: 0, from: 10));

    /// <summary>
    ///     Where a screen starting again begins: the first row of what is picked out, so that what a reader was
    ///     reading comes back at the top of the page rather than at the foot of it (#84).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    [InlineData(99)]
    public void Begins_IsTheFirstRowOfWhatIsPickedOut(int at) =>
        Assert.Equal(at, Scroll.Begins(Rows(100, at, tall: 3)));

    /// <summary>
    ///     Which is what <see cref="Scroll.To" /> would not have answered: it brings the selection into view from
    ///     where the scroll already is, and from the top that puts a selection below the page at its foot — a reader
    ///     thrown backwards a screenful by the very key that was meant to leave them where they were.
    /// </summary>
    [Fact]
    public void Begins_IsAboveWhereScrollingToItFromTheTopWouldLand()
    {
        var lines = Rows(100, at: 40, tall: 3);

        Assert.Equal(40, Scroll.Begins(lines));
        Assert.Equal(23, Scroll.To(lines, 20, from: 0));
    }

    /// <summary>A screen with nothing picked out begins at its own top, which is where it always began.</summary>
    [Fact]
    public void Begins_IsTheTopWhereNothingIsPickedOut() =>
        Assert.Equal(0, Scroll.Begins(Rows(100, at: -1, tall: 0)));

    /// <summary>
    ///     How far into the topmost post the page has got, which is the other half of a reader's place: nought where
    ///     the page begins exactly on a post, and the rows of it scrolled past otherwise (#84).
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    [InlineData(9, 9)]
    [InlineData(10, 0)]
    [InlineData(37, 7)]
    public void Into_CountsTheRowsOfTheTopmostPostAlreadyScrolledPast(int from, int expected) =>
        Assert.Equal(expected, Scroll.Into(Numbered(items: 5, tall: 10), from));

    /// <summary>
    ///     An offset past the last row — which the arrows can leave, since they are not held to the rows there are —
    ///     is measured against the rows there are now rather than answering about a page nobody can see.
    /// </summary>
    [Fact]
    public void Into_MeasuresAnOffsetPastTheEndAgainstTheRowsThereAre() =>
        Assert.Equal(9, Scroll.Into(Numbered(items: 2, tall: 10), from: 400));

    /// <summary>A screen with no posts on it has no post to be part way into.</summary>
    [Fact]
    public void Into_IsNoughtWhereTheScreenHoldsNoItems() =>
        Assert.Equal(0, Scroll.Into([Line.Blank, Line.Blank], from: 1));

    /// <summary>What the arrows do: one row at a time, and never off either end of the rows there are.</summary>
    [Theory]
    [InlineData(30, 1, 31)]
    [InlineData(30, -1, 29)]
    [InlineData(0, -1, 0)]
    [InlineData(99, 1, 99)]
    public void By_MovesTheScreenARowAndStopsAtEitherEnd(int from, int rows, int expected) =>
        Assert.Equal(expected, Scroll.By(Rows(100, at: 0, tall: 3), from, rows));

    /// <summary>
    ///     An offset left over from a taller screen is brought back within the rows there are, which is what asking to
    ///     move by nothing is for: rows are worked out afresh every frame, and a post taken down shortens them.
    /// </summary>
    [Fact]
    public void By_ClampsAnOffsetLeftOverFromMoreRowsThanThereAreNow() =>
        Assert.Equal(9, Scroll.By(Rows(10, at: 0, tall: 3), from: 400, rows: 0));

    /// <summary>
    ///     Whether the selection is still on the page, which is what settles whether <c>j</c> moves or reclaims. Its
    ///     first row and its last are both enough — a post whose middle is showing has not been scrolled away from.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(3, true)]
    [InlineData(22, true)]
    [InlineData(23, false)]
    public void Shows_SaysWhetherAnyRowOfTheSelectionIsOnThePage(int from, bool expected) =>
        Assert.Equal(expected, Scroll.Shows(Rows(100, at: 20, tall: 3), height: 20, from));

    /// <summary>A screen with nothing picked out shows no selection at any offset, and nothing to reclaim either.</summary>
    [Fact]
    public void Shows_IsFalseWhereNothingIsPickedOut() =>
        Assert.False(Scroll.Shows(Rows(100, at: -1, tall: 0), height: 20, from: 0));

    /// <summary>The topmost item on the page is the one <c>j</c> reclaims, by the ordinal its rows were named with.</summary>
    [Fact]
    public void Topmost_NamesTheItemTheFirstRowOnThePageBelongsTo() =>
        Assert.Equal(3, Scroll.Topmost(Numbered(items: 5, tall: 10), from: 30));

    /// <summary>
    ///     A post whose top has been scrolled off but which still has rows on the page counts as the topmost one, so
    ///     that <c>↓ ↓ ↓ j</c> selects the post being read rather than the one after it.
    /// </summary>
    [Fact]
    public void Topmost_NamesAPostWhoseTopHasBeenScrolledOff() =>
        Assert.Equal(3, Scroll.Topmost(Numbered(items: 5, tall: 10), from: 35));

    /// <summary>
    ///     A row belonging to no item — a heading, a notice, the blank between two posts — is not an item, so the
    ///     answer is the first one under it rather than whatever was above.
    /// </summary>
    [Fact]
    public void Topmost_LooksPastRowsThatBelongToNoItem() =>
        Assert.Equal(0, Scroll.Topmost([Line.Blank, Line.Blank, .. Numbered(items: 2, tall: 3)], from: 0));

    /// <summary>
    ///     Scrolled to the very foot of a screen, where what is left is the blank after the last post, the answer is
    ///     that last post — not nothing, which would put <c>j</c> back to moving from a post that is off the page.
    /// </summary>
    [Fact]
    public void Topmost_LooksBackUpWhereNothingIsAtOrBelowTheOffset() =>
        Assert.Equal(1, Scroll.Topmost([.. Numbered(items: 2, tall: 3), Line.Blank], from: 6));

    /// <summary>A screen with no items on it at all has nothing to reclaim, and <c>j</c> moves as it always does.</summary>
    [Fact]
    public void Topmost_AnswersNothingWhereTheScreenHoldsNoItems() =>
        Assert.Null(Scroll.Topmost([Line.Blank, Line.Blank], from: 1));

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

    /// <summary><paramref name="items" /> posts of <paramref name="tall" /> rows each, every row naming its own.</summary>
    private static IReadOnlyList<Line> Numbered(int items, int tall) =>
        [
            .. Enumerable.Range(0, items * tall)
                         .Select(row => Line.Of("text", Role.Body).PartOf(row / tall)),
        ];
}
