using Terminal.Gui.Input;
using Wooly.Tests.Fakes;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;

namespace Wooly.Tests.Tui;

/// <summary>
///     The page following a walked reference (#473): walking to a reference that is not on the page moves the page the
///     least it must to bring that reference onto it, as <c>j</c> and <c>k</c> bring the pick back.
/// </summary>
public class ReferenceFollowTests
{
    /// <summary>How many rows the shell is drawn in: a frame row over the page, and a frame row and the status row under it.</summary>
    private const int Height = 16;

    /// <summary>The terminal row the page begins on, under the content panel's top edge.</summary>
    private const int PageTop = 1;

    /// <summary>The terminal row the page ends on, over the content panel's bottom edge and the status row.</summary>
    private const int PageBottom = Height - 3;

    /// <summary>Twenty rows of a post's text with no reference in them.</summary>
    private static readonly string Filler = string.Concat(
        Enumerable.Range(1, 20).Select(line => $"Line {line} of the fleece\n"));

    /// <summary>A post far taller than the terminal, with a hashtag at its top, one in its middle and one at its foot.</summary>
    private static readonly string Tall = $"Shearing #one\n{Filler}Carding #two\n{Filler}Spinning #three";

    /// <summary>The shell drawn on a feed whose first post reads <paramref name="content" />.</summary>
    private static async Task<DrawnShell> Drawn(string content, int width = 80)
    {
        var built = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", content: content), APost.With(id: "220")),
        };

        return await DrawnShell.Of(width, Height, Themes.Plain, built);
    }

    /// <summary>
    ///     On a post taller than the terminal, <c>→</c> to a reference below the page's bottom row brings it onto that
    ///     bottom row and no further.
    /// </summary>
    [Fact]
    public async Task RightToAReferenceBelowThePageBringsItOntoTheBottomRow()
    {
        using var drawn = await Drawn(Tall);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Equal("#two", drawn.Shell.Screen.Reference?.Text);
        Assert.Equal(PageBottom, RowOf(drawn, "‹#two›"));
    }

    /// <summary><c>←</c> to a reference above the page's top row brings it onto that top row and no further.</summary>
    [Fact]
    public async Task LeftToAReferenceAboveThePageBringsItOntoTheTopRow()
    {
        using var drawn = await Drawn(Tall);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Equal(PageBottom, RowOf(drawn, "‹#three›"));

        drawn.Press(Key.CursorLeft);

        Assert.Equal("#two", drawn.Shell.Screen.Reference?.Text);
        Assert.Equal(PageTop, RowOf(drawn, "‹#two›"));
    }

    /// <summary>A reference wrapped across two rows is brought onto the page whole: both its brackets are drawn.</summary>
    [Fact]
    public async Task AWrappedReferenceIsBroughtOnWhole()
    {
        const string address = "https://example.com/notes/on/shearing/and/carding/and/spinning";

        using var drawn = await Drawn($"Shearing #one\n{Filler}{address}", width: 60);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Equal(address, drawn.Shell.Screen.Reference?.Text);

        var opening = RowOf(drawn, "‹");
        var closing = RowOf(drawn, "›");

        Assert.True(closing > opening, string.Join('\n', drawn.Rows()));
        Assert.Equal(PageBottom, closing);
    }

    /// <summary>Walking to a reference already on the page leaves the page where it is.</summary>
    [Fact]
    public async Task AReferenceAlreadyOnThePageLeavesThePageWhereItIs()
    {
        using var drawn = await Drawn($"Shearing #one\nCarding #two\n{Filler}");

        drawn.Press(Key.CursorRight);

        var before = drawn.Rows();

        drawn.Press(Key.CursorRight);

        Assert.Equal("#two", drawn.Shell.Screen.Reference?.Text);
        Assert.Equal(RowOf(drawn, "Shearing"), Array.FindIndex(before, row => row.Contains("Shearing")));
        Assert.Equal(before[PageBottom], drawn.Rows()[PageBottom]);
    }
}
