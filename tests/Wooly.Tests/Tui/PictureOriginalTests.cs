using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The Picture screen draws the instance's original rather than the smaller copy: the smaller copy at once, the
///     original in the same cells once it lands, and the smaller copy left standing with a word on the status row where
///     the original will not come (#517, ADR-0034). Read back off the whole shell drawn headless in the no-colour theme,
///     its cells pinned at 10×20.
/// </summary>
public class PictureOriginalTests
{
    /// <summary>
    ///     A picture opened is sent for at its original, on screen, beside the smaller copy it is drawn at until the
    ///     original lands — so that the smaller copy, already held for the timeline, stays held while it stands in.
    /// </summary>
    [Fact]
    public async Task ThePictureScreenSendsForTheOriginalBesideTheSmallerCopy()
    {
        var pictures = new FakePictures().Holding("m1", 400, 300);

        using var drawn = await Opened(APost.APicture("m1", shape: new(400, 300)), pictures);

        Assert.Contains(Wanted(pictures), wanted => wanted.Drawn.Id == Original("m1").Id && wanted.OnScreen);
        Assert.Contains(Wanted(pictures), wanted => wanted.Drawn.Id == "m1" && wanted.OnScreen);
    }

    /// <summary>
    ///     The smaller copy is drawn until the original lands, and the original then in its place: in the same box,
    ///     with nothing outside it changed (story 12).
    /// </summary>
    [Fact]
    public async Task TheOriginalReplacesTheSmallerCopyWithoutAnythingMoving()
    {
        var pictures = new FakePictures().Holding("m1", 400, 300);

        using var drawn = await Opened(
            APost.APicture("m1", shape: new(400, 300), originalShape: new(2000, 1500)),
            pictures);

        var box = Box(drawn);
        var rows = drawn.Rows();

        Assert.Equal("m1", Showing(drawn).PictureId);

        pictures.Holding(Original("m1").Id, 1000, 750);
        drawn.Redraw();

        Assert.Equal(Original("m1").Id, Showing(drawn).PictureId);
        Assert.Equal(box, Box(drawn));
        Assert.Equal(rows, drawn.Rows());
    }

    /// <summary>
    ///     The box is settled from the original's shape where the instance said it, the smaller copy's standing in
    ///     only where it did not: a square original makes a square box whatever the smaller copy was said to be — 25
    ///     rows tall at 10×20 cells and twice that across, as #516's is — where the wide shape said of the smaller copy
    ///     would have made a box three rows tall.
    /// </summary>
    [Fact]
    public async Task TheBoxIsSettledFromTheOriginalsShape()
    {
        using var drawn = await Opened(
            APost.APicture("m1", shape: new(4000, 400), originalShape: new(1000, 1000)),
            new FakePictures().Holding("m1", 400, 400));

        Assert.Equal(new System.Drawing.Rectangle(24, 1, 50, 25), Box(drawn));
    }

    /// <summary>
    ///     An original too large to fetch, or one that could not be fetched or read, leaves the smaller copy drawn and
    ///     the status row saying why (stories 18 and 19).
    /// </summary>
    [Theory]
    [InlineData(Unshowable.TooLarge, "The original is too large to show.")]
    [InlineData(Unshowable.Failed, "The original could not be fetched.")]
    public async Task AnOriginalThatWillNotComeLeavesTheSmallerCopyAndSaysWhy(Unshowable why, string said)
    {
        var pictures = new FakePictures().Holding("m1", 400, 300).Refusing(Original("m1").Id, why);

        using var drawn = await Opened(APost.APicture("m1", shape: new(400, 300)), pictures);

        Assert.Equal("m1", Showing(drawn).PictureId);
        Assert.Contains(said, drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Leaving the Picture screen leaves its original wanted by no frame, which is what lets the cache let go of it
    ///     (story 20), and takes the word about it off the status row; the smaller copy is still wanted by the feed.
    /// </summary>
    [Fact]
    public async Task LeavingThePictureScreenWantsTheOriginalNoMore()
    {
        var pictures = new FakePictures().Holding("m1", 400, 300).Refusing(Original("m1").Id, Unshowable.TooLarge);

        using var drawn = await Opened(APost.APicture("m1", shape: new(400, 300)), pictures);

        drawn.Press(Key.Esc);
        drawn.Redraw();

        Assert.DoesNotContain(Wanted(pictures), wanted => wanted.Drawn.Id == Original("m1").Id);
        Assert.Contains(Wanted(pictures), wanted => wanted.Drawn.Id == "m1");
        Assert.DoesNotContain("too large", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     The shell on an 80×30 terminal drawing sixel at 10×20 cells in the no-colour theme, reading a post with
    ///     <paramref name="picture" /> on it, the picture walked to and opened on its Picture screen.
    /// </summary>
    private static async Task<DrawnShell> Opened(PostMedia picture, FakePictures pictures)
    {
        var drawn = await DrawnShell.Of(
            80,
            30,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(APost.With(id: "110", media: [picture]), APost.With(id: "220")),
            },
            pictures: pictures,
            drawsPictures: true,
            raster: ARaster.Sixel());

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.IsType<PictureScreen>(drawn.Shell.Screen);

        return drawn;
    }

    /// <summary>The original of the attachment <paramref name="id" /> names, at whatever size it is drawn.</summary>
    private static Drawn Original(string id) => Drawn.Original(APost.APicture(id), 0, 0);

    /// <summary>What the latest frame wanted.</summary>
    private static IReadOnlyList<WantedPicture> Wanted(FakePictures pictures) => pictures.Frames[^1];

    /// <summary>The picture showing.</summary>
    private static PictureView Showing(DrawnShell drawn) =>
        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);

    /// <summary>The cells the picture showing is drawn in, on the terminal.</summary>
    private static System.Drawing.Rectangle Box(DrawnShell drawn) => Showing(drawn).FrameToScreen();
}
