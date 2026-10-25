using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     A walked-to reference that scrolls off is let go (#478): the moment none of its rows are on the page, whatever
///     moved or reflowed it — an arrow, a page key, the wheel or a resize — the brackets go and the status row leaves
///     reference mode. Scrolling back does not bring it back, and the picked thing stays where it is.
/// </summary>
/// <remarks>
///     Pressed, wheeled and resized on the whole shell drawn headless, because what a reader sees is the brackets and
///     the status row: whether either is still offering <c>⏎ open</c> for something off the page.
/// </remarks>
public class ReferenceLetGoTests
{
    /// <summary>How many rows the shell is drawn in: a frame row over the page, and a frame row and the status row under it.</summary>
    private const int Height = 16;

    /// <summary>The terminal row the page ends on, over the content panel's bottom edge and the status row.</summary>
    private const int PageBottom = Height - 3;

    /// <summary>What the status row says only while a reference is picked.</summary>
    private const string ReferenceMode = "Open: ⏎";

    /// <summary>Twenty rows of a post's text with no reference in them.</summary>
    private static readonly string Filler = string.Concat(
        Enumerable.Range(1, 20).Select(line => $"Line {line} of the fleece\n"));

    /// <summary>A post far taller than the terminal, with a hashtag at its top, one in its middle and one at its foot.</summary>
    private static readonly string Tall = $"Shearing #one\n{Filler}Carding #two\n{Filler}Spinning #three";

    /// <summary>The shell drawn on a feed whose first post reads <paramref name="content" />.</summary>
    private static async Task<DrawnShell> Drawn(string content, int width = 80, AShell? built = null)
    {
        built ??= new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", content: content), APost.With(id: "220")),
        };

        return await DrawnShell.Of(width, Height, Themes.Plain, built);
    }

    /// <summary>Nothing held: no reference, no brackets, and a status row out of reference mode.</summary>
    private static void HoldsNothing(DrawnShell drawn)
    {
        var rows = drawn.Rows();

        Assert.Null(drawn.Shell.Screen.Reference);
        Assert.DoesNotContain(rows, row => row.Contains('‹'));
        Assert.DoesNotContain(rows, row => row.Contains(ReferenceMode, StringComparison.Ordinal));
    }

    /// <summary>
    ///     The post's last reference walked to with <c>→</c>, the page following it onto the bottom row — rather than
    ///     entered with <c>←</c>, which enters at the last reference on the page (#477), not the post's last.
    /// </summary>
    private static void WalkToTheFoot(DrawnShell drawn)
    {
        drawn.Press(Key.CursorRight);

        for (var before = drawn.Shell.Screen.Reference; ; before = drawn.Shell.Screen.Reference)
        {
            drawn.Press(Key.CursorRight);

            if (Equals(drawn.Shell.Screen.Reference, before))
            {
                return;
            }
        }
    }

    /// <summary>
    ///     <c>↓</c> pressed until the reference's row leaves the top of the page lets it go — and not a press before,
    ///     while the row still shows. The pick stays on the post.
    /// </summary>
    [Fact]
    public async Task DownUntilTheReferenceLeavesThePageLetsItGo()
    {
        using var drawn = await Drawn(Tall);

        drawn.Press(Key.CursorRight);

        Assert.Equal("#one", drawn.Shell.Screen.Reference?.Text);

        while (RowOf(drawn, "‹#one›") - 3 >= 1)
        {
            drawn.Press(Key.CursorDown);

            Assert.Equal("#one", drawn.Shell.Screen.Reference?.Text);
        }

        drawn.Press(Key.CursorDown);

        HoldsNothing(drawn);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary><c>↑</c> taking a reference off the foot of the page lets it go.</summary>
    [Fact]
    public async Task UpTakingTheReferenceOffTheFootLetsItGo()
    {
        using var drawn = await Drawn(Tall);

        WalkToTheFoot(drawn);

        Assert.Equal(PageBottom, RowOf(drawn, "‹#three›"));

        drawn.Press(Key.CursorUp);

        HoldsNothing(drawn);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary><c>PgDn</c> and <c>PgUp</c> turning the page away from the reference let it go.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APageKeyTurningTheReferenceOffLetsItGo(bool down)
    {
        using var drawn = await Drawn(Tall);

        if (down)
        {
            drawn.Press(Key.CursorRight);
        }
        else
        {
            WalkToTheFoot(drawn);
        }

        Assert.NotNull(drawn.Shell.Screen.Reference);

        drawn.Press(down ? Key.PageDown : Key.PageUp);

        HoldsNothing(drawn);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>
    ///     Drawn as the running loop draws a key — only what it changed, onto the frame before — the status row leaves
    ///     reference mode in the very frame that took the reference off, not one frame late.
    /// </summary>
    [Fact]
    public async Task TheStatusRowSaysSoInTheSameFrame()
    {
        using var drawn = await Drawn(Tall);

        drawn.Press(Key.CursorRight);

        Assert.Contains(drawn.Rows(), row => row.Contains(ReferenceMode, StringComparison.Ordinal));

        drawn.PressAsTheLoopDraws(Key.PageDown);

        HoldsNothing(drawn);
    }

    /// <summary>The wheel, a notch at a time, lets the reference go on the notch that takes its row off.</summary>
    [Fact]
    public async Task TheWheelTakingTheReferenceOffLetsItGo()
    {
        using var drawn = await Drawn(Tall);

        drawn.Press(Key.CursorRight);

        for (var notch = 0; notch < Height && drawn.Rows().Any(row => row.Contains("‹#one›")); notch++)
        {
            Assert.True(RowOf(drawn, "‹#one›") >= 1);

            drawn.Wheel(OverContent, 5);
        }

        HoldsNothing(drawn);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
    }

    /// <summary>A resize that takes the reference's row off the page lets it go.</summary>
    [Fact]
    public async Task AResizeThatTakesTheReferenceOffLetsItGo()
    {
        using var drawn = await Drawn(Tall);

        WalkToTheFoot(drawn);

        Assert.Equal(PageBottom, RowOf(drawn, "‹#three›"));

        drawn.Resize(80, Height - 4);

        HoldsNothing(drawn);
    }

    /// <summary>A resize that leaves the reference's row on the page keeps it.</summary>
    [Fact]
    public async Task AResizeThatLeavesTheReferenceOnKeepsIt()
    {
        using var drawn = await Drawn(Tall);

        WalkToTheFoot(drawn);
        drawn.Resize(80, Height + 4);

        Assert.Equal("#three", drawn.Shell.Screen.Reference?.Text);
        Assert.True(RowOf(drawn, "‹#three›") >= 1);
    }

    /// <summary>An address wrapped across two rows is kept while one of them still shows, and let go once neither does.</summary>
    [Fact]
    public async Task AWrappedAddressIsKeptWhileEitherRowShows()
    {
        const string address = "https://example.com/notes/on/shearing/and/carding/and/spinning";

        using var drawn = await Drawn($"Shearing #one\n{Filler}{address}", width: 60);

        WalkToTheFoot(drawn);

        Assert.Equal(address, drawn.Shell.Screen.Reference?.Text);
        Assert.Equal(PageBottom, RowOf(drawn, "›"));
        Assert.Equal(PageBottom - 1, RowOf(drawn, "‹"));

        drawn.Wheel(OverContent, 5, down: false);

        Assert.Equal(address, drawn.Shell.Screen.Reference?.Text);
        Assert.Equal(PageBottom, RowOf(drawn, "‹"));

        drawn.Wheel(OverContent, 5, down: false);

        HoldsNothing(drawn);
    }

    /// <summary>Scrolling back to where the reference was does not bring it back.</summary>
    [Fact]
    public async Task ScrollingBackDoesNotRestoreIt()
    {
        using var drawn = await Drawn(Tall);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.PageDown);

        HoldsNothing(drawn);

        drawn.Press(Key.PageUp);

        Assert.True(RowOf(drawn, "#one") >= 1);
        HoldsNothing(drawn);
    }

    /// <summary>Once it is let go, <c>⏎</c> reads the post, as it does with no reference picked, and opens nothing.</summary>
    [Fact]
    public async Task EnterAfterwardsReadsThePost()
    {
        using var drawn = await Drawn(Tall);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.PageDown);
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);
        Assert.Empty(drawn.Built.Browser.Opened);
    }

    /// <summary>Once it is let go, <c>esc</c> goes back rather than being spent letting go of something unseen.</summary>
    [Fact]
    public async Task EscAfterwardsGoesBack()
    {
        using var drawn = await Drawn(Tall);

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal(2, drawn.Shell.Depth);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.PageDown);

        HoldsNothing(drawn);

        drawn.Press(Key.Esc);
        drawn.Settle();

        Assert.Equal(1, drawn.Shell.Depth);
    }

    /// <summary>
    ///     A reference is let go once its own rows are off the page, even where another post that reads the same at the
    ///     same place has come onto it: that copy is not the one walked to.
    /// </summary>
    [Fact]
    public async Task AnotherPostsCopyOfTheReferenceDoesNotKeepIt()
    {
        var filler = string.Concat(Enumerable.Range(1, 1).Select(line => $"Line {line} of the fleece\n"));
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(
                APost.With(id: "110", content: "#wool on the first"),
                APost.With(id: "220", content: filler),
                APost.With(id: "330", content: "#wool on the last")),
        };

        using var drawn = await DrawnShell.Of(80, 24, Themes.Plain, built);

        drawn.Press(Key.CursorRight);
        Assert.Equal("#wool", drawn.Shell.Screen.Reference?.Text);

        Assert.Contains(drawn.Rows()[..^2], row => row.Contains("on the last", StringComparison.Ordinal));

        while (drawn.Rows()[..^2].Any(row => row.Contains("on the first", StringComparison.Ordinal)))
        {
            drawn.Press(Key.CursorDown);
        }

        Assert.Contains(drawn.Rows()[..^2], row => row.Contains("on the last", StringComparison.Ordinal));
        Assert.Null(drawn.Shell.Screen.Reference);
    }
}
