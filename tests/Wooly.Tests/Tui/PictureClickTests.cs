using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     A drawn picture answers the pointer as every other <b>Reference</b> does: a click anywhere on its box, or in the
///     columns beside it its brackets are drawn in, walks to it, and a double click there opens it on its
///     <b>Picture screen</b> (review of #514, ADR-0034). Read back off the whole shell drawn headless in the no-colour
///     theme, because what a reader sees is the brackets and the crumb.
/// </summary>
public class PictureClickTests
{
    /// <summary>
    ///     The first column a screen draws in on a 100-column terminal: just past the rail's 20 columns, the panel's left
    ///     edge and its margin — the column a picture's left bracket is drawn in.
    /// </summary>
    private const int Left = RailLines.Width + 2;

    /// <summary>The last column a screen draws in there, which a picture's right bracket is drawn in.</summary>
    private const int Right = 97;

    /// <summary>Every glyph a walked picture's brackets are drawn in, either side, a one-row box's included.</summary>
    private const string Brackets = "⎡⎢⎣⎤⎥⎦[]";

    /// <summary>Wider than any content region, so that the region's width is what bounds the box.</summary>
    private static readonly PictureShape Wide = new(4000, 600);

    /// <summary>
    ///     A click on the middle of a picture's box, on its first row, on its last, or in either bracket column beside
    ///     it, walks to it and brackets it — with its pixels landed, which leave most of the box's rows blank, and
    ///     before they have.
    /// </summary>
    [Theory]
    [InlineData(true, "middle")]
    [InlineData(true, "top")]
    [InlineData(true, "foot")]
    [InlineData(true, "left")]
    [InlineData(true, "right")]
    [InlineData(false, "middle")]
    [InlineData(false, "right")]
    public async Task AClickAnywhereOnAPicturesBoxWalksToItAndBracketsIt(bool landed, string where)
    {
        using var drawn = await Drawn(landed);

        var (top, foot) = Box(drawn);
        var (column, row) = where switch
        {
            "top" => (Left + 20, top),
            "foot" => (Right - 20, foot),
            "left" => (Left, (top + foot) / 2),
            "right" => (Right, (top + foot) / 2),
            _ => ((Left + Right) / 2, (top + foot) / 2),
        };

        drawn.Click(column, row);
        drawn.Redraw();

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.Equal(('⎡', '⎤'), Glyphs(drawn, top));
        Assert.Equal(('⎣', '⎦'), Glyphs(drawn, foot));
        Assert.Contains(drawn.Rows(), row => row.Contains("Open: ⏎", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A double click on a picture's box opens it on its Picture screen, in a feed and on the post screen alike, and
    ///     hands the browser nothing.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADoubleClickOnAPicturesBoxOpensItsPictureScreen(bool onThePostScreen)
    {
        using var drawn = await Drawn(landed: true);

        if (onThePostScreen)
        {
            drawn.Press(Key.Enter);
            drawn.Settle();

            Assert.IsType<PostScreen>(drawn.Shell.Screen);
        }

        var (top, foot) = Box(drawn);

        drawn.DoubleClick((Left + Right) / 2, (top + foot) / 2);
        drawn.Settle();

        Assert.Contains(drawn.Rows(), row => row.Contains("Picture by @maria@fosstodon.org", StringComparison.Ordinal));
        Assert.Contains(drawn.Rows(), row => row.Contains("esc back", StringComparison.Ordinal));
        Assert.Empty(drawn.Built.Browser.Opened);
    }

    /// <summary>
    ///     A double click on a post's text, above its picture, still opens the post rather than the picture, and a single
    ///     click there picks the post and brackets nothing.
    /// </summary>
    [Fact]
    public async Task ADoubleClickOnAPostsTextStillOpensThePost()
    {
        using var drawn = await Drawn(landed: true);

        var text = Array.FindIndex(drawn.Rows(), row => row.Contains("Shearing day", StringComparison.Ordinal));

        drawn.Click(Left + 4, text);
        drawn.Redraw();

        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.DoesNotContain(drawn.Rows(), row => row.IndexOfAny(['⎡', '⎣']) >= 0);

        drawn.DoubleClick(Left + 4, text);
        drawn.Settle();

        Assert.Contains(drawn.Rows(), row => row.Contains("Post by @maria@fosstodon.org", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("Picture by", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The shell on a 100-column terminal drawing pictures in the no-colour theme, reading a post of Maria's with one
    ///     wide picture under its text, and one more post under it — the picture's pixels landed or not.
    /// </summary>
    private static Task<DrawnShell> Drawn(bool landed) =>
        DrawnShell.Of(
            100,
            40,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(
                    APost.With(
                        id: "110",
                        account: "maria@fosstodon.org",
                        content: "Shearing day",
                        media: [APost.APicture("m1", description: "A sheep", shape: Wide)]),
                    APost.With(id: "220")),
            },
            pictures: landed ? new FakePictures().Holding("m1", Wide.Width, Wide.Height) : new FakePictures(),
            drawsPictures: true);

    /// <summary>
    ///     The first and last rows of the picture's box, found by walking to it with <c>→</c> and letting it go again
    ///     with <c>esc</c>, which leave the page as it was.
    /// </summary>
    private static (int Top, int Foot) Box(DrawnShell drawn)
    {
        drawn.Press(Key.CursorRight);

        var rows = Enumerable.Range(0, drawn.Application.Driver!.Contents!.GetLength(0))
            .Where(row => Brackets.Contains(At(drawn, row, Left)))
            .ToList();

        drawn.Press(Key.Esc);

        Assert.True(rows.Count > 2, string.Join('\n', drawn.Rows()));
        Assert.DoesNotContain(drawn.Rows(), row => row.IndexOfAny(['⎡', '⎣']) >= 0);

        return (rows[0], rows[^1]);
    }

    /// <summary>What is drawn in the two bracket columns of <paramref name="row" />.</summary>
    private static (char Left, char Right) Glyphs(DrawnShell drawn, int row) =>
        (At(drawn, row, Left), At(drawn, row, Right));

    /// <summary>The one character drawn at <paramref name="column" /> of <paramref name="row" />.</summary>
    private static char At(DrawnShell drawn, int row, int column) =>
        drawn.Application.Driver!.Contents![row, column].Grapheme is [var only] ? only : '\0';
}
