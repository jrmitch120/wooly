using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>⏎</c> on a walked picture opens it on a <b>Picture screen</b>: the one picture fitted whole inside the window,
///     its description beneath it and a hint row under that, and <c>esc</c> back to where it was opened from with the
///     same picture walked (#516, ADR-0034). Read back off the whole shell drawn headless in the no-colour theme, its
///     cells pinned at 10×20 so that a box's size can be worked out by hand.
/// </summary>
public class PictureScreenTests
{
    /// <summary>
    ///     The first column a screen draws in on an 80-column terminal: just past the rail's 20 columns and the panel's
    ///     left edge.
    /// </summary>
    private const int ContentLeft = RailLines.Width + 1;

    /// <summary>The column past the last one a screen draws in there, which is its margin.</summary>
    private const int ContentRight = 78;

    /// <summary>Wider than any content region, so that the region's width is what bounds the box.</summary>
    private static readonly PictureShape Wide = new(4000, 400);

    [Fact]
    public async Task EnterOnAWalkedPictureOpensItsPictureScreenCrumbedByThePostsAuthor()
    {
        using var drawn = await Reading(APost.With(
            id: "110",
            account: "maria@fosstodon.org",
            media: [APost.APicture("m1", shape: Wide)]));

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PictureScreen>(drawn.Shell.Screen);
        Assert.Equal(2, drawn.Shell.Depth);
        Assert.Equal("Picture by @maria@fosstodon.org", drawn.Shell.Crumbs[^1]);
        Assert.Empty(drawn.Built.Browser.Opened);
    }

    /// <summary>
    ///     What its author said the picture shows is drawn beneath it, wrapped onto as many rows as it takes rather than
    ///     cut, and the hint row under that says <c>esc</c> goes back.
    /// </summary>
    [Fact]
    public async Task TheDescriptionIsWrappedBeneathThePictureAndTheHintRowUnderIt()
    {
        const string said = "A cartoon sheep standing in a field of clover, looking at the reader with an expression "
            + "of mild surprise, a fence behind it";

        using var drawn = await Opened(APost.With(
            id: "110",
            media: [APost.APicture("m1", description: said, shape: Wide)]));

        var rows = drawn.Rows();
        var first = Array.FindIndex(rows, row => row.Contains("A cartoon sheep standing", StringComparison.Ordinal));
        var hint = Array.FindIndex(rows, row => row.Contains("esc back", StringComparison.Ordinal));

        Assert.True(first > 0, string.Join('\n', rows));
        Assert.Equal(Box(drawn).Bottom, first);
        Assert.True(hint > first + 1, string.Join('\n', rows));
        Assert.Equal(
            said,
            string.Join(' ', rows[first..hint].Select(row => row[ContentLeft..ContentRight].Trim())));
    }

    /// <summary>
    ///     The picture is as large as the window less the description's row and the hint row allows, fitted whole at its
    ///     own proportions and centred across: a square one at 10×20 cells is twice as many columns as rows, as tall as
    ///     the 27 rows inside the panel less those two, and a wide one the panel's 57 columns across. Never cropped, never
    ///     stretched out of shape.
    /// </summary>
    [Theory]
    [InlineData(400, 400, 24, 1, 50, 25)]
    [InlineData(4000, 400, 21, 1, 57, 3)]
    public async Task ThePictureIsFittedWholeIntoTheWindowLessTheDescriptionAndHintRows(
        int wide,
        int tall,
        int left,
        int top,
        int columns,
        int rows)
    {
        using var drawn = await Opened(APost.With(id: "110", media: [APost.APicture("m1", shape: new(wide, tall))]));

        Assert.Equal(new System.Drawing.Rectangle(left, top, columns, rows), Box(drawn));
        Assert.Contains("A cartoon sheep", drawn.Rows()[top + rows], StringComparison.Ordinal);
        Assert.Contains("esc back", drawn.Rows()[top + rows + 1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>esc</c> goes back at its first press, to the feed as it was left: the same post picked, the same picture
    ///     walked between its brackets, the same page and the same status row.
    /// </summary>
    [Fact]
    public async Task EscGoesBackToTheScreenUnderneathWithItsPickAndWalkedPictureUnchanged()
    {
        using var drawn = await Reading(APost.With(
            id: "110",
            content: "Shearing #wool",
            media:
            [
                APost.APicture("m1", description: "The first sheep", shape: Wide),
                APost.APicture("m2", description: "The second sheep", shape: Wide),
            ]));

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        var walked = drawn.Rows();

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Contains(drawn.Rows(), row => row.Contains("The second sheep", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("The first sheep", StringComparison.Ordinal));

        drawn.Press(Key.Esc);

        Assert.Equal(1, drawn.Shell.Depth);
        Assert.Equal(walked, drawn.Rows());
        Assert.Contains(walked, row => row[ContentLeft + 1] == '⎡');
    }

    /// <summary>
    ///     A window resized while the screen is open draws the picture again to fit the new one: at 120×40 a square
    ///     picture is the 37 rows inside the panel less two tall and twice that across, centred in the 97 columns.
    /// </summary>
    [Fact]
    public async Task AResizeRedrawsThePictureToFitTheNewWindow()
    {
        using var drawn = await Opened(APost.With(id: "110", media: [APost.APicture("m1", shape: new(400, 400))]));

        drawn.Resize(120, 40);

        Assert.Equal(new System.Drawing.Rectangle(34, 1, 70, 35), Box(drawn));
        Assert.Contains("esc back", drawn.Rows()[37], StringComparison.Ordinal);
    }

    /// <summary>
    ///     A picture whose original is not here yet shows its <b>Stand-in</b> in the box until it arrives, and then the
    ///     picture in the same cells.
    /// </summary>
    [Fact]
    public async Task APictureNotHeldShowsItsStandInAndThenThePictureInTheSameBox()
    {
        var pictures = new FakePictures();
        var picture = APost.APicture("m1", shape: new(400, 400), blurhash: StandInBlurTests.AHash);

        using var drawn = await Opened(APost.With(id: "110", media: [picture]), pictures);

        var standing = Box(drawn);

        Assert.Equal(
            Drawn.Blur(Drawn.Original(picture, 0, 0)).Id,
            Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible).PictureId);

        pictures.HoldingOriginal("m1", 400, 400);
        drawn.Redraw();

        Assert.Equal(
            Drawn.Original(picture, 0, 0).Id,
            Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible).PictureId);
        Assert.Equal(standing, Box(drawn));
    }

    /// <summary>
    ///     A picture walked to on the post screen opens as one walked to in a feed does, and <c>esc</c> comes back to the
    ///     post screen with it still walked.
    /// </summary>
    [Fact]
    public async Task APictureOpensFromThePostScreenAsFromAFeed()
    {
        using var drawn = await Reading(APost.With(
            id: "110",
            account: "maria@fosstodon.org",
            media: [APost.APicture("m1", shape: Wide)]));

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PostScreen>(drawn.Shell.Screen);

        drawn.Press(Key.CursorRight);

        var walked = drawn.Rows();

        Assert.Contains(walked, row => row.Contains('⎡'));

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PictureScreen>(drawn.Shell.Screen);
        Assert.Equal("Picture by @maria@fosstodon.org", drawn.Shell.Crumbs[^1]);
        Assert.Contains(drawn.Rows(), row => row.Contains("esc back", StringComparison.Ordinal));

        drawn.Press(Key.Esc);

        Assert.IsType<PostScreen>(drawn.Shell.Screen);
        Assert.Equal(walked, drawn.Rows());
    }

    /// <summary>A picture its author gave no description says so where the description would be.</summary>
    [Fact]
    public async Task APictureWithNoDescriptionSaysItIsUndescribed()
    {
        using var drawn = await Opened(APost.With(
            id: "110",
            media: [APost.APicture("m1", description: null, shape: Wide)]));

        Assert.Contains(drawn.Rows(), row => row.Contains("a picture, undescribed", StringComparison.Ordinal));
    }

    /// <summary>
    ///     The shell on an 80×<paramref name="height" /> terminal drawing sixel at 10×20 cells in the no-colour theme,
    ///     reading <paramref name="post" /> and one more under it, with its pictures already here unless a test says
    ///     otherwise.
    /// </summary>
    private static Task<DrawnShell> Reading(Post post, FakePictures? pictures = null, int height = 30) =>
        DrawnShell.Of(
            80,
            height,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(post, APost.With(id: "220")) },
            pictures: pictures ?? Holding(post),
            drawsPictures: true,
            raster: ARaster.Sixel());

    /// <summary>
    ///     <see cref="Reading" />, with the first picture on <paramref name="post" /> walked to and opened on its Picture
    ///     screen.
    /// </summary>
    private static async Task<DrawnShell> Opened(Post post, FakePictures? pictures = null, int height = 30)
    {
        var drawn = await Reading(post, pictures, height);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.Enter);
        drawn.Settle();

        return drawn;
    }

    /// <summary>The cells the picture showing is drawn in, on the terminal.</summary>
    private static System.Drawing.Rectangle Box(DrawnShell drawn) =>
        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible).FrameToScreen();

    /// <summary>
    ///     Every picture on <paramref name="post" /> here, its smaller copy and its original alike, at the shape the
    ///     instance reported for it.
    /// </summary>
    private static FakePictures Holding(Post post) => post.Media.Aggregate(
        new FakePictures(),
        (pictures, media) => pictures
            .Holding(media.Id, media.Shape?.Width ?? 1600, media.Shape?.Height ?? 900)
            .HoldingOriginal(media.Id, media.Shape?.Width ?? 1600, media.Shape?.Height ?? 900));
}
