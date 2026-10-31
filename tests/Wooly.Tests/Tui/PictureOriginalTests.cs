using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The Picture screen draws the instance's original rather than the smaller copy: an empty box until the original
///     lands, the original then in the same cells, and the smaller copy only where the original will not come, with a
///     word under the picture saying why (#517, ADR-0034, review of #514). Read back off the whole shell drawn headless
///     in the no-colour theme, its cells pinned at 10×20.
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
    ///     The box is left blank until the original lands, though the smaller copy is already held for the timeline and
    ///     the instance sent a blurhash: no smaller copy, no blur, no shade. The original is then drawn in those cells,
    ///     with the description under it where it was (review of #514).
    /// </summary>
    [Fact]
    public async Task TheBoxIsBlankUntilTheOriginalLandsInTheSameCells()
    {
        var pictures = new FakePictures().Holding("m1", 400, 300);

        using var drawn = await Opened(
            APost.APicture(
                "m1",
                shape: new(400, 300),
                blurhash: StandInBlurTests.AHash,
                originalShape: new(2000, 1500)),
            pictures);

        var waiting = drawn.Rows();

        Assert.DoesNotContain(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible);

        pictures.Holding(Original("m1").Id, 1000, 750);
        drawn.Redraw();

        var box = Box(drawn);

        Assert.Equal(Original("m1").Id, Showing(drawn).PictureId);
        Assert.All(
            waiting[box.Top..box.Bottom],
            row => Assert.Equal(new string(' ', box.Width), row[box.Left..box.Right]));
        Assert.Contains("A cartoon sheep", waiting[box.Bottom], StringComparison.Ordinal);
        Assert.Equal(waiting[box.Bottom], drawn.Rows()[box.Bottom]);
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
            new FakePictures().HoldingOriginal("m1", 1000, 1000));

        Assert.Equal(new System.Drawing.Rectangle(24, 1, 50, 25), Box(drawn));
    }

    /// <summary>
    ///     An original too large to fetch, or one that could not be fetched or read, has the smaller copy drawn in its
    ///     place, the screen wanting it now, and says why on the row under the description, centred as it is; the
    ///     status row goes on saying the keys, so that <c>esc</c> is never hidden behind it (stories 18 and 19, review
    ///     of #514).
    /// </summary>
    [Theory]
    [InlineData(Unshowable.TooLarge, "The original is too large to show.")]
    [InlineData(Unshowable.Failed, "The original could not be fetched.")]
    public async Task AnOriginalThatWillNotComeFallsBackToTheSmallerCopyAndSaysWhyUnderIt(Unshowable why, string said)
    {
        var pictures = new FakePictures().Holding("m1", 400, 300).Refusing(Original("m1").Id, why);

        using var drawn = await Opened(
            APost.APicture("m1", shape: new(400, 300), blurhash: StandInBlurTests.AHash),
            pictures);

        var rows = drawn.Rows();
        var box = Box(drawn);

        Assert.Equal("m1", Showing(drawn).PictureId);
        Assert.Contains(Wanted(pictures), wanted => wanted.Drawn.Id == "m1" && wanted.OnScreen);
        Assert.Contains("A cartoon sheep", rows[box.Bottom], StringComparison.Ordinal);
        Assert.Equal(said, Content(rows[box.Bottom + 1]).Trim());
        Assert.Equal(Centred(said), Content(rows[box.Bottom + 1]).TrimEnd());
        Assert.Contains("Back: esc", rows[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(said, rows[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     The row why is said in is kept under the description whether anything is said in it or not, so that an
    ///     original refused once the screen is drawn moves nothing: the box stays where it was, and the description
    ///     on the row under it.
    /// </summary>
    [Fact]
    public async Task SayingWhyAnOriginalWillNotComeMovesNothing()
    {
        var pictures = new FakePictures().Holding("m1", 400, 300);

        using var drawn = await Opened(APost.APicture("m1", shape: new(400, 300)), pictures);

        var waiting = drawn.Rows();

        pictures.Refusing(Original("m1").Id, Unshowable.Failed);
        drawn.Redraw();

        var rows = drawn.Rows();
        var box = Box(drawn);

        Assert.Equal(waiting[..box.Bottom], rows[..box.Bottom]);
        Assert.Contains("A cartoon sheep", rows[box.Bottom], StringComparison.Ordinal);
        Assert.Equal(waiting[box.Bottom], rows[box.Bottom]);
        Assert.Equal(string.Empty, Content(waiting[box.Bottom + 1]).Trim());
        Assert.Equal("The original could not be fetched.", Content(rows[box.Bottom + 1]).Trim());
    }

    /// <summary>
    ///     Leaving the Picture screen leaves its original wanted by no frame, which is what lets the cache let go of it
    ///     (story 20), and nothing said of it is left behind; the smaller copy is still wanted by the feed.
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
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("too large", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A step to the post's next picture draws that picture's original and still wants the one stepped off, so that
    ///     a step back finds it held rather than sending for it again — and says nothing of the one stepped off, which
    ///     is not the one drawn now, until it is stepped back to (#518, review of #514).
    /// </summary>
    [Fact]
    public async Task SteppingToAnotherPictureKeepsTheOneSteppedOffWantedAndSaysWhyOfTheOneShownAlone()
    {
        var pictures = new FakePictures()
            .Holding("m1", 400, 300)
            .Holding("m2", 400, 300)
            .Refusing(Original("m1").Id, Unshowable.TooLarge);

        using var drawn = await Opened(
            [APost.APicture("m1", shape: new(400, 300)), APost.APicture("m2", shape: new(400, 300))],
            pictures);

        Assert.Contains(drawn.Rows(), row => row.Contains("too large", StringComparison.Ordinal));

        drawn.Press(Key.CursorRight);
        drawn.Redraw();

        Assert.Contains(Wanted(pictures), wanted => wanted.Drawn.Id == Original("m2").Id && wanted.OnScreen);
        Assert.Contains(Wanted(pictures), wanted => wanted.Drawn.Id == Original("m1").Id);
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("too large", StringComparison.Ordinal));

        drawn.Press(Key.CursorLeft);

        Assert.Contains(drawn.Rows(), row => row.Contains("too large", StringComparison.Ordinal));
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

    /// <summary>
    ///     The columns of <paramref name="row" /> a screen draws in on an 80-column terminal: past the rail's 20 and the
    ///     panel's left edge, and short of its margin.
    /// </summary>
    private static string Content(string row) => row[(RailLines.Width + 1)..78];

    /// <summary><paramref name="said" /> centred in the 57 columns a screen draws in there.</summary>
    private static string Centred(string said) => new string(' ', (57 - said.Length) / 2) + said;
}
