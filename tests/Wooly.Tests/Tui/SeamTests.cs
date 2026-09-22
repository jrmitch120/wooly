using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     What divides one region from the next: the column between the rail and the content, and the blank row under
///     the breadcrumb. The column is drawn, because a cell nothing paints is a cell Terminal.Gui paints in a grey no
///     theme chose; the row is not, because the page showing through is exactly what blank means (#216).
/// </summary>
/// <remarks>
///     The diagram in <c>docs/tui-shell.md</c> has drawn the column since the contract was written and nothing ever
///     painted it. Its division is carried without colour by the rule down it, so a terminal answering
///     <c>NO_COLOR</c> still has the rail told from the content; the row's is carried by its being blank, which is
///     what every screen already does between one thing and the next.
/// </remarks>
public class SeamTests
{
    /// <summary>
    ///     The column beside the content is a rule, one column wide, all the way down — a glyph rather than a
    ///     background alone, so that the rail is still divided from the content where there is no colour to divide
    ///     them with.
    /// </summary>
    [Fact]
    public void Gutter_IsARuleOneColumnWideAllTheWayDown()
    {
        var rows = ChromeLines.Gutter(6);

        Assert.Equal(6, rows.Count);
        Assert.All(rows, row => Assert.Equal("│", row.Text));
        Assert.All(rows, row => Assert.Equal(1, row.Width));
        Assert.All(rows, row => Assert.All(row.Spans, span => Assert.Equal(Role.Seam, span.Role)));
    }

    /// <summary>
    ///     The rule is a glyph and not a colour, so it is there whatever the theme is — which is the whole of why the
    ///     column carries one. Asserted against the theme a terminal answering <c>NO_COLOR</c> gets, where every role
    ///     resolves to the same pair and a background divides nothing from anything.
    /// </summary>
    [Fact]
    public async Task Gutter_DividesTheRailFromTheContentWithNoColourToDivideThemWith()
    {
        using var window = await Opened(Themes.Plain);

        var gutter = Region(window, x: RailLines.Width, y: 0);

        // Nothing the theme answers tells the seam from the page here, which is what NO_COLOR means.
        Assert.Equal(Themes.Plain.For(Role.Body), Themes.Plain.For(Role.Seam));

        Assert.All(ChromeLines.Gutter(gutter.Frame.Height), row => Assert.Equal("│", row.Text));
    }

    /// <summary>
    ///     Both are where the contract puts them: the row between the breadcrumb and the content, which no region
    ///     covers, and the column between the rail and everything to its right, from the top down to the status row.
    /// </summary>
    [Fact]
    public async Task Shell_DividesEveryRegionFromTheNext()
    {
        using var window = await Opened(Themes.Dark);

        var breadcrumb = Region(window, x: RailLines.Width + 1, y: 0);
        var gutter = Region(window, x: RailLines.Width, y: 0);
        var content = window.SubViews.OfType<PaintedView>().Single(view => view.Id == ShellWindow.ContentId);

        // Row 0 is the breadcrumb's, row 1 is nobody's, and the content starts under both. A region drawing the row
        // is what this asserts the absence of: the page showing through is what makes it blank.
        Assert.Equal(1, breadcrumb.Frame.Height);
        Assert.Equal(2, content.Frame.Y);
        Assert.DoesNotContain(window.SubViews.OfType<PaintedView>(), view => view.Frame is { X: > 0, Y: 1 });

        // And the column runs from the top to the status row, which is the height the rail runs to.
        Assert.Equal(0, gutter.Frame.Y);
        Assert.Equal(window.Frame.Height - 1, gutter.Frame.Height);
    }

    /// <summary>
    ///     A cell no region covers is still the theme's rather than Terminal.Gui's. Nothing is left over now that the
    ///     seams are painted, so this is the backstop: a layout change that opens a new hole opens it onto the page
    ///     rather than onto the toolkit's own grey.
    /// </summary>
    /// <remarks>
    ///     Asserted against <see cref="Themes.Dark" /> rather than <see cref="Themes.Plain" />, whose every answer is
    ///     the terminal's default and so would pass without the window having been told anything.
    /// </remarks>
    [Fact]
    public async Task Shell_PaintsWhateverNoRegionCoversInTheThemesOwnPage()
    {
        using var window = await Opened(Themes.Dark);

        Assert.Equal(Themes.Dark.For(Role.Body), window.GetScheme().Normal);
    }

    /// <summary>The one region whose top-left corner is <paramref name="x" />, <paramref name="y" />, laid out.</summary>
    private static PaintedView Region(ShellWindow window, int x, int y) =>
        window.SubViews.OfType<PaintedView>().Single(view => view.Frame.X == x && view.Frame.Y == y);

    /// <summary>A window over a shell showing one post, laid out at an 80-column terminal.</summary>
    private static async Task<ShellWindow> Opened(ITheme theme)
    {
        var built = new AShell { Timelines = FakeTimelineReader.Holding(APost.With(id: "220")) };
        var shell = await built.Opened();

        var window = new ShellWindow(shell, theme, built.Clock, () => { }, FakePictures.DrawingNothing())
        {
            Width = 80,
            Height = 24,
        };

        window.Layout();

        return window;
    }
}
