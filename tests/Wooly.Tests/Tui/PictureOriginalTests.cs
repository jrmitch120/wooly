using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The Picture screen draws the instance's original rather than the smaller copy: its <b>Stand-in</b> until the
///     original lands, the original then in the same cells, and the smaller copy only where the original will not
///     come, with a word on the status row (#517, ADR-0034). Read back off the whole shell drawn headless in the
///     no-colour theme, its cells pinned at 10×20.
/// </summary>
public class PictureOriginalTests
{
    /// <summary>
    ///     A picture opened is sent for at its original, on screen, and the screen does not want the smaller copy
    ///     beside it: drawing that and then swapping the original in reads as the picture flashing.
    /// </summary>
    [Fact]
    public async Task ThePictureScreenSendsForTheOriginalAlone()
    {
        var pictures = new FakePictures().Holding("m1", 400, 300);

        using var drawn = await Opened(APost.APicture("m1", shape: new(400, 300)), pictures);

        Assert.Contains(Wanted(pictures), wanted => wanted.Drawn.Id == Original("m1").Id && wanted.OnScreen);
        Assert.DoesNotContain(Wanted(pictures), wanted => wanted.Drawn.Id == "m1");
    }

    /// <summary>
    ///     The Stand-in is drawn until the original lands, though the smaller copy is already held for the timeline,
    ///     and the original then in its place: in the same box, with the description under it where it was.
    /// </summary>
    [Fact]
    public async Task TheStandInIsDrawnUntilTheOriginalLandsInTheSameCells()
    {
        var pictures = new FakePictures().Holding("m1", 400, 300);

        using var drawn = await Opened(
            APost.APicture(
                "m1",
                shape: new(400, 300),
                blurhash: StandInBlurTests.AHash,
                originalShape: new(2000, 1500)),
            pictures);

        var box = Box(drawn);
        var described = drawn.Rows()[box.Bottom];

        Assert.Equal(Drawn.Blur(Original("m1")).Id, Showing(drawn).PictureId);
        Assert.Contains("A cartoon sheep", described, StringComparison.Ordinal);

        pictures.Holding(Original("m1").Id, 1000, 750);
        drawn.Redraw();

        Assert.Equal(Original("m1").Id, Showing(drawn).PictureId);
        Assert.Equal(box, Box(drawn));
        Assert.Equal(described, drawn.Rows()[box.Bottom]);
    }

    /// <summary>
    ///     The box is settled from the original's shape where the instance said it, the smaller copy's standing in
    ///     only where it did not: a square original makes a square box whatever the smaller copy was said to be — 26
    ///     rows tall at 10×20 cells and twice that across, as #516's is — where the wide shape said of the smaller copy
    ///     would have made a box three rows tall.
    /// </summary>
    [Fact]
    public async Task TheBoxIsSettledFromTheOriginalsShape()
    {
        using var drawn = await Opened(
            APost.APicture("m1", shape: new(4000, 400), originalShape: new(1000, 1000)),
            new FakePictures().HoldingOriginal("m1", 1000, 1000));

        Assert.Equal(new System.Drawing.Rectangle(23, 1, 52, 26), Box(drawn));
    }

    /// <summary>
    ///     An original too large to fetch, or one that could not be fetched or read, has the smaller copy drawn in its
    ///     place, the screen wanting it now, and the status row saying why (stories 18 and 19).
    /// </summary>
    [Theory]
    [InlineData(Unshowable.TooLarge, "The original is too large to show.")]
    [InlineData(Unshowable.Failed, "The original could not be fetched.")]
    public async Task AnOriginalThatWillNotComeFallsBackToTheSmallerCopyAndSaysWhy(Unshowable why, string said)
    {
        var pictures = new FakePictures().Holding("m1", 400, 300).Refusing(Original("m1").Id, why);

        using var drawn = await Opened(
            APost.APicture("m1", shape: new(400, 300), blurhash: StandInBlurTests.AHash),
            pictures);

        Assert.Equal("m1", Showing(drawn).PictureId);
        Assert.Contains(Wanted(pictures), wanted => wanted.Drawn.Id == "m1" && wanted.OnScreen);
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
    ///     A step to the post's next picture sends for that picture's original and wants the one stepped off no more,
    ///     so that it is let go of then rather than when the screen goes — and takes off the status row what was said of
    ///     the one stepped off, which is not the one drawn now (#518).
    /// </summary>
    [Fact]
    public async Task SteppingToAnotherPictureWantsItsOriginalInsteadAndTakesBackWhatWasSaid()
    {
        var pictures = new FakePictures()
            .Holding("m1", 400, 300)
            .Holding("m2", 400, 300)
            .Refusing(Original("m1").Id, Unshowable.TooLarge);

        using var drawn = await Opened(
            [APost.APicture("m1", shape: new(400, 300)), APost.APicture("m2", shape: new(400, 300))],
            pictures);

        Assert.Contains("too large", drawn.Rows()[^1], StringComparison.Ordinal);

        drawn.Press(Key.CursorRight);
        drawn.Redraw();

        Assert.Contains(Wanted(pictures), wanted => wanted.Drawn.Id == Original("m2").Id && wanted.OnScreen);
        Assert.DoesNotContain(Wanted(pictures), wanted => wanted.Drawn.Id == Original("m1").Id);
        Assert.DoesNotContain("too large", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     The shell on an 80×30 terminal drawing sixel at 10×20 cells in the no-colour theme, reading a post with
    ///     <paramref name="picture" /> on it, the picture walked to and opened on its Picture screen.
    /// </summary>
    private static Task<DrawnShell> Opened(PostMedia picture, FakePictures pictures) => Opened([picture], pictures);

    /// <summary>
    ///     The shell as <see cref="Opened(PostMedia, FakePictures)" /> draws it, reading a post with
    ///     <paramref name="media" /> on it and the first of them opened.
    /// </summary>
    private static async Task<DrawnShell> Opened(IReadOnlyList<PostMedia> media, FakePictures pictures)
    {
        var drawn = await DrawnShell.Of(
            80,
            30,
            Themes.Plain,
            new AShell
            {
                Timelines = FakeTimelineReader.Holding(APost.With(id: "110", media: [.. media]), APost.With(id: "220")),
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
