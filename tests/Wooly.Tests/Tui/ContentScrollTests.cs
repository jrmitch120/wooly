using Terminal.Gui.ViewBase;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     The content region's own scroll position, which is the half of #51 that cannot live in <see cref="Scroll" />:
///     only a view knows how much room there is, so only a view can be asked whether the selection is still on the
///     page. Built with no driver and no run loop — nothing here draws, and everything here is a fact about rows.
/// </summary>
public class ContentScrollTests
{
    /// <summary>Ten rows of room, over two posts of ten rows each, with the first of them picked out.</summary>
    private const int Room = 10;

    /// <summary>The arrows move the screen, and go on moving it until the selection is off the page.</summary>
    [Fact]
    public void Step_WalksTheScreenAwayFromTheSelection()
    {
        var content = Content();

        Assert.Null(content.Reclaimable);

        // Nine rows down the selection's last row is still the top one on the page; the tenth carries it off.
        for (var walked = 0; walked < 9; walked++)
        {
            content.Step(1);
        }

        Assert.Null(content.Reclaimable);

        content.Step(1);

        Assert.Equal(1, content.Reclaimable);
    }

    /// <summary>And they walk it back again, which puts the selection where it was.</summary>
    [Fact]
    public void Step_WalksBackUpAndStopsAtTheTop()
    {
        var content = Content();

        content.Step(10);

        Assert.Equal(1, content.Reclaimable);

        // Further up than there is anywhere to go, to prove it stops rather than running past the first row.
        content.Step(-40);

        Assert.Null(content.Reclaimable);
    }

    /// <summary>
    ///     A screen being replaced by one nobody has read yet starts again at the top, rather than at a row offset
    ///     made on somebody else's rows — the pushed screen would otherwise open half way down.
    /// </summary>
    [Fact]
    public void Resume_AtTheTopStartsTheScreenAgain()
    {
        var content = Content();

        content.Step(10);
        content.Resume(0, following: true);

        Assert.Null(content.Reclaimable);
        Assert.Equal(0, content.Top);
        Assert.True(content.Following);
    }

    /// <summary>
    ///     And a screen walked back out to gets its page back where the reader left it: the rows are the very rows the
    ///     offset was made on, which is the one replacement that is not somebody else's list (#133).
    /// </summary>
    [Fact]
    public void Resume_PutsTheScrollBackWhereItWasLeft()
    {
        var content = Content();

        content.Step(10);

        Assert.Equal(10, content.Top);
        Assert.False(content.Following);

        // What was drilled into took the region to the top of its own rows; this is the walk back out to what the
        // screen underneath was left at.
        content.Resume(0, following: true);
        content.Resume(10, following: false);

        Assert.Equal(10, content.Top);
        Assert.Equal(1, content.Reclaimable);

        // Carried as well as the row: a reader who had walked the page away from the pick comes back to what they
        // were looking at rather than being snapped onto it.
        Assert.False(content.Following);
    }

    /// <summary>
    ///     A page is a screenful and no more: what was under the last row of one page is the first row of the next.
    ///     It used to be ten posts, which on a feed with pictures on it is several screens at a time.
    /// </summary>
    [Fact]
    public void Turn_MovesTheScreenByExactlyOneScreenful()
    {
        var content = Laid(new PaintedView(Themes.Plain, (_, _) => Numbered()) { Scrolls = true });

        content.Turn(1);

        Assert.Equal(Room, content.Reclaimable);

        content.Turn(1);

        Assert.Equal(Room * 2, content.Reclaimable);

        content.Turn(-1);

        Assert.Equal(Room, content.Reclaimable);
    }

    /// <summary>
    ///     A region that does not scroll — the rail, the breadcrumb, the status row — has no offset to move and
    ///     nothing to reclaim, whatever is pressed at it.
    /// </summary>
    [Fact]
    public void Step_DoesNothingToARegionThatDoesNotScroll()
    {
        var chrome = Laid(new PaintedView(Themes.Plain, (_, _) => Rows()));

        chrome.Step(10);

        Assert.Null(chrome.Reclaimable);
    }

    /// <summary>The content region, laid out at ten rows of room over the rows below.</summary>
    private static PaintedView Content() =>
        Laid(new PaintedView(Themes.Plain, (_, _) => Rows()) { Scrolls = true });

    /// <summary>
    ///     A hundred rows, each its own item and none of them picked out — so that
    ///     <see cref="PaintedView.Reclaimable" /> reads back the row the screen starts at, which is otherwise the
    ///     view's own business. With nothing selected there is always something to reclaim, and the topmost item at an
    ///     offset is the row at it.
    /// </summary>
    private static IReadOnlyList<Line> Numbered() =>
        [.. Enumerable.Range(0, 100).Select(row => Line.Of("text", Role.Body).PartOf(row))];

    /// <summary>Two posts of ten rows each, the first picked out — a screen twice as tall as the room for it.</summary>
    private static IReadOnlyList<Line> Rows() =>
        [
            .. Enumerable.Range(0, 20)
                         .Select(row => Line.Of("text", row < 10 ? Role.Selection : Role.Body).PartOf(row / 10)),
        ];


    /// <summary>The view at a size, since a view with no frame has no room and answers nothing.</summary>
    private static PaintedView Laid(PaintedView view)
    {
        view.Width = 20;
        view.Height = Room;

        view.Layout();

        return view;
    }
}
