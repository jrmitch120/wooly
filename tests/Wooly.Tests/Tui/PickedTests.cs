using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The things on a screen with one of them picked out: the index, the clamp at either end, and the rows the
///     picked one is drawn on.
/// </summary>
/// <remarks>
///     The invariant that matters is that the rows and the index count the same things in the same order. Nothing
///     catches a screen that stamps its rows wrongly at compile time — <see cref="Scroll" /> finds what is picked by
///     <see cref="Role.Selection" /> and the topmost thing by <see cref="Line.Item" />, so a screen numbering its rows
///     one way and its picks another breaks scrolling in a module it never touches (#51). Held once here, so that it
///     is asserted once rather than six times over.
/// </remarks>
public class PickedTests
{
    /// <summary>Three things, drawn one row each so that a row and a thing are easy to tell apart from a blank.</summary>
    private static Picked<string> Three() => new(["one", "two", "three"]);

    /// <summary>Every thing draws its own name, and says how much room it was given.</summary>
    private static IReadOnlyList<Line> Draw(string thing, int at, int room) =>
        [Line.Of($"{thing} at {at} in {room}", Role.Body)];

    /// <summary>
    ///     The rows marked as picked are exactly the rows saying they are part of the thing picked out — the one
    ///     property a screen cannot get wrong without breaking scrolling somewhere else.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Rows_MarksTheSameThingItNames(int at)
    {
        var picked = Three();

        picked.Pick(at);

        var lines = picked.Rows(61, Draw);

        Assert.Equal(
            lines.Where(line => line.Item == at).ToList(),
            lines.Where(line => line.Has(Role.Selection)).ToList());

        Assert.Contains(lines, line => line.Item == at);
    }

    /// <summary>Every thing on the list is named, so that no row of one is unaccounted for.</summary>
    [Fact]
    public void Rows_NamesEveryThingOnTheList()
    {
        var items = Three().Rows(61, Draw).Select(line => line.Item).OfType<int>().Distinct().Order();

        Assert.Equal([0, 1, 2], items);
    }

    /// <summary>The blank between two things belongs to neither, so a page that begins on one begins on a thing.</summary>
    [Fact]
    public void Rows_LeavesTheBlankBetweenTwoThingsPartOfNeither()
    {
        var lines = Three().Rows(61, Draw);

        Assert.Equal(6, lines.Count);
        Assert.All(lines.Where(line => line.Spans.Count == 0), line => Assert.Null(line.Item));
    }

    /// <summary>
    ///     One column of gutter is always taken, whether or not anything on the row is picked — so that moving the
    ///     pick down does not shift every thing sideways as it goes.
    /// </summary>
    [Fact]
    public void Rows_TakeTheGutterColumnWhetherOrNotTheyArePickedOut()
    {
        var lines = Three().Rows(61, Draw).Where(line => line.Item is not null).ToList();

        Assert.All(lines, line => Assert.Equal(1, line.Spans[0].Width));
        Assert.All(lines, line => Assert.Contains(" at ", line.Text));
        Assert.Equal(1, lines.Count(line => line.Has(Role.Selection)));
    }

    /// <summary>What a thing is drawn in is the width less the gutter, asked here rather than by every screen.</summary>
    [Fact]
    public void Rows_HandAThingTheRoomLeftBesideTheGutter()
    {
        Assert.Contains("in 60", Three().Rows(61, Draw)[0].Text);
    }

    /// <summary>A step off either end stops there: a list you walked off the end of is one you have lost your place in.</summary>
    [Fact]
    public void Move_StopsAtEitherEnd()
    {
        var picked = Three();

        picked.Move(1);

        Assert.Equal(1, picked.At);

        picked.Move(-5);

        Assert.Equal(0, picked.At);

        picked.Move(9);

        Assert.Equal(2, picked.At);
    }

    /// <summary>
    ///     <c>Home</c> and <c>End</c> ask to move by the largest step there is, which added to an index overflows back
    ///     to the other end of the list.
    /// </summary>
    [Fact]
    public void Move_DoesNotWrapRoundOnTheLargestStepThereIs()
    {
        var picked = Three();

        picked.Move(int.MaxValue);

        Assert.Equal(2, picked.At);

        picked.Move(int.MinValue);

        Assert.Equal(0, picked.At);
    }

    /// <summary>A number off either end is clamped the same way stepping off the end is (#51).</summary>
    [Fact]
    public void Pick_StopsAtEitherEnd()
    {
        var picked = Three();

        picked.Pick(int.MaxValue);

        Assert.Equal(2, picked.At);

        picked.Pick(int.MinValue);

        Assert.Equal(0, picked.At);
    }

    /// <summary>A list with nothing on it has nothing picked, which is a fact about the list rather than a place in it.</summary>
    [Fact]
    public void Out_IsNothingWhereThereIsNothingOnTheList()
    {
        var picked = new Picked<string>([]);

        picked.Move(1);
        picked.Pick(2);

        Assert.Null(picked.Out);
        Assert.Equal(0, picked.At);
        Assert.Empty(picked.Rows(61, Draw));
    }

    /// <summary>Walking picks the thing out, which is what every key that acts on something acts on.</summary>
    [Fact]
    public void Out_IsTheThingWalkedTo()
    {
        var picked = Three();

        picked.Move(2);

        Assert.Equal("three", picked.Out);
    }

    /// <summary>Something put at the end lands where a message this profile has just sent belongs in its thread.</summary>
    [Fact]
    public void Add_PutsAThingAtTheEnd()
    {
        var picked = Three();

        picked.Add("four");

        Assert.Equal(["one", "two", "three", "four"], picked.All);
    }

    /// <summary>Each thing is walked once and put back changed, which is the only walk of the list there is.</summary>
    [Fact]
    public void Rewrite_PutsTheChangedThingBackInPlace()
    {
        var picked = Three();

        picked.Rewrite(thing => thing == "two" ? "TWO" : thing);

        Assert.Equal(["one", "TWO", "three"], picked.All);
    }

    /// <summary>
    ///     Taking things off cannot leave the pick past the end of what is left — the rows are worked out afresh
    ///     every frame, and an index past them is a screen with nothing picked and no way to say so.
    /// </summary>
    [Fact]
    public void Remove_KeepsThePickInsideWhatIsLeft()
    {
        var picked = Three();

        picked.Move(2);
        picked.Remove(thing => thing == "three");

        Assert.Equal(1, picked.At);
        Assert.Equal("two", picked.Out);
    }

    /// <summary>An emptied list is back at the start, rather than at whichever index it was left on.</summary>
    [Fact]
    public void Remove_GoesBackToTheStartWhenNothingIsLeft()
    {
        var picked = Three();

        picked.Move(2);
        picked.Remove(_ => true);

        Assert.Equal(0, picked.At);
        Assert.Null(picked.Out);
    }

    /// <summary>
    ///     One thing's rows on their own, stamped the same way: what a screen splicing headings between them asks for,
    ///     so that interleaving something is still not stamping anything.
    /// </summary>
    [Fact]
    public void RowsOf_StampsOneThingAndLeavesTheBlankToTheScreen()
    {
        var picked = Three();

        picked.Pick(1);

        var lines = picked.RowsOf(1, 61, Draw);

        Assert.Equal(1, Assert.Single(lines).Item);
        Assert.True(lines[0].Has(Role.Selection));
        Assert.Equal(
            picked.Rows(61, Draw).Where(line => line.Item == 1).Select(line => line.Text),
            lines.Select(line => line.Text));
    }
}
