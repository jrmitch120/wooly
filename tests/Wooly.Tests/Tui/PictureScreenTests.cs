using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>⏎</c> on a walked picture opens it on a <b>Picture screen</b>: the one picture fitted whole inside the window,
///     its description centred beneath it, and <c>esc</c> back to where it was opened from with the same post picked and
///     nothing walked (#516, ADR-0034, review of #514). Read back off the whole shell drawn headless in the no-colour
///     theme, its cells pinned at 10×20 so that a box's size can be worked out by hand.
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

    /// <summary>Every glyph a walked picture's brackets are drawn in, either side, a one-row box's included.</summary>
    private static readonly char[] Brackets = "⎡⎢⎣⎤⎥⎦".ToCharArray();

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
    ///     What its author said the picture shows is drawn beneath it, wrapped onto the rows a description this short
    ///     takes rather than cut, each row centred under the picture, and nothing under it: the status row already says
    ///     the keys.
    /// </summary>
    [Fact]
    public async Task TheDescriptionIsWrappedAndCentredBeneathThePictureWithNothingUnderIt()
    {
        const string said = "A cartoon sheep standing in a field of clover, looking at the reader with an expression "
            + "of mild surprise, a fence behind it";

        using var drawn = await Opened(APost.With(
            id: "110",
            media: [APost.APicture("m1", description: said, shape: Wide)]));

        var rows = drawn.Rows();
        var first = Box(drawn).Bottom;
        var content = rows[1..^2].Select(row => row[ContentLeft..ContentRight]).ToList();
        var described = content[(first - 1)..].TakeWhile(row => row.Trim().Length > 0).ToList();

        Assert.True(described.Count > 1, string.Join('\n', rows));
        Assert.Equal(said, string.Join(' ', described.Select(row => row.Trim())));
        Assert.All(
            described,
            row => Assert.InRange((row.Length - row.TrimStart().Length) - (row.Length - row.TrimEnd().Length), -1, 1));
        Assert.All(content[(first - 1 + described.Count)..], row => Assert.Equal(string.Empty, row.Trim()));
    }

    /// <summary>
    ///     "a picture, undescribed", where its author said nothing, is centred under the picture as a description is:
    ///     its 22 columns 17 into the 57 the screen draws in.
    /// </summary>
    [Fact]
    public async Task UndescribedIsCentredUnderThePicture()
    {
        using var drawn = await Opened(APost.With(
            id: "110",
            media: [APost.APicture("m1", description: null, shape: Wide)]));

        Assert.Equal(
            new string(' ', 17) + "a picture, undescribed",
            drawn.Rows()[Box(drawn).Bottom][ContentLeft..ContentRight].TrimEnd());
    }

    /// <summary>
    ///     The picture is as large as the window less the description's row allows, fitted whole at its own proportions
    ///     and centred across: a square one at 10×20 cells is twice as many columns as rows, as tall as the 27 rows
    ///     inside the panel less that one, and a wide one the panel's 57 columns across. Never cropped, never stretched
    ///     out of shape.
    /// </summary>
    [Theory]
    [InlineData(400, 400, 23, 1, 52, 26)]
    [InlineData(4000, 400, 21, 1, 57, 3)]
    public async Task ThePictureIsFittedWholeIntoTheWindowLessTheDescriptionsRows(
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
    }

    /// <summary>
    ///     A description as long as a whole article — as a post bridged from a blog can carry, HTML and all — never
    ///     squeezes out the picture: it is cut to a quarter of the 27 rows inside the panel, six, its last row ending in
    ///     <c>…</c> where it is cut and written as it came, and the picture takes the rest, a square one 21 rows tall.
    /// </summary>
    [Fact]
    public async Task ALongDescriptionIsCutSoThatThePictureKeepsItsRoom()
    {
        var article = "<article><h1>On shearing</h1>"
            + string.Concat(Enumerable.Repeat("<p>The flock came down from the hill at first light, wet.</p>", 60))
            + "</article>";

        using var drawn = await Opened(APost.With(
            id: "110",
            media: [APost.APicture("m1", description: article, shape: new(400, 400))]));

        var rows = drawn.Rows();
        var box = Box(drawn);
        var described = rows[box.Bottom..(box.Bottom + 6)]
            .Select(row => row[ContentLeft..ContentRight].TrimEnd())
            .ToList();

        Assert.Equal(new System.Drawing.Rectangle(28, 1, 42, 21), box);
        Assert.StartsWith("<article><h1>On shearing</h1>", described[0].Trim(), StringComparison.Ordinal);
        Assert.EndsWith("…", described[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("…", string.Concat(described[..^1]), StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>esc</c> goes back at its first press, to the feed with the same post picked and nothing walked: opening the
    ///     picture let go of it as opening a link does (#474), so no brackets are drawn and the page and status row are
    ///     as they were before the walk began (review of #514).
    /// </summary>
    [Fact]
    public async Task EscGoesBackToTheScreenUnderneathWithThePostPickedAndNothingWalked()
    {
        using var drawn = await Reading(APost.With(
            id: "110",
            content: "Shearing #wool",
            media:
            [
                APost.APicture("m1", description: "The first sheep", shape: Wide),
                APost.APicture("m2", description: "The second sheep", shape: Wide),
            ]));

        var unwalked = drawn.Rows();

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        Assert.Contains(drawn.Rows(), row => row[ContentLeft + 1] == '⎡');

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Contains(drawn.Rows(), row => row.Contains("The second sheep", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("The first sheep", StringComparison.Ordinal));

        drawn.Press(Key.Esc);

        Assert.Equal(1, drawn.Shell.Depth);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.DoesNotContain(drawn.Rows(), row => row.IndexOfAny(Brackets) >= 0);
        Assert.Equal(unwalked, drawn.Rows());
    }

    /// <summary>
    ///     A window resized while the screen is open draws the picture again to fit the new one: at 120×40 a square
    ///     picture is the 37 rows inside the panel less one tall and twice that across, centred in the 97 columns.
    /// </summary>
    [Fact]
    public async Task AResizeRedrawsThePictureToFitTheNewWindow()
    {
        using var drawn = await Opened(APost.With(id: "110", media: [APost.APicture("m1", shape: new(400, 400))]));

        drawn.Resize(120, 40);

        Assert.Equal(new System.Drawing.Rectangle(33, 1, 72, 36), Box(drawn));
        Assert.Contains("A cartoon sheep", drawn.Rows()[37], StringComparison.Ordinal);
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
    ///     post screen with nothing walked, as it was before the walk began.
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

        var unwalked = drawn.Rows();

        drawn.Press(Key.CursorRight);

        Assert.Contains(drawn.Rows(), row => row.Contains('⎡'));

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PictureScreen>(drawn.Shell.Screen);
        Assert.Equal("Picture by @maria@fosstodon.org", drawn.Shell.Crumbs[^1]);

        drawn.Press(Key.Esc);

        Assert.IsType<PostScreen>(drawn.Shell.Screen);
        Assert.Equal("110", drawn.Shell.Screen.Picked?.Id);
        Assert.DoesNotContain(drawn.Rows(), row => row.IndexOfAny(Brackets) >= 0);
        Assert.Equal(unwalked, drawn.Rows());
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
