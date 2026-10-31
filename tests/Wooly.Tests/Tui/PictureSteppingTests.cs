using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>←</c> and <c>→</c> on a <b>Picture screen</b> step to the post's previous and next drawn still picture, in the
///     order they are drawn, stopping at either end; the hint row says which of how many is showing, and <c>esc</c> comes
///     back with the picture it was opened on still walked, whichever was stepped to (#518, ADR-0034). Read back off the
///     whole shell drawn headless in the no-colour theme.
/// </summary>
public class PictureSteppingTests
{
    /// <summary>Wider than any content region, so that each box is a few rows tall and the post fits on the page.</summary>
    private static readonly PictureShape Wide = new(4000, 400);

    /// <summary>Three pictures with a video between the first two, which the walk passes over as a picture.</summary>
    private static Post ThreePicturesAndAVideo => APost.With(
        id: "110",
        media:
        [
            APost.APicture("m1", description: "The first sheep", shape: Wide),
            APost.Attached(MediaKind.Video, "v1", description: "A sheep running", shape: Wide),
            APost.APicture("m2", description: "The second sheep", shape: Wide),
            APost.APicture("m3", description: "The third sheep", shape: Wide),
        ]);

    /// <summary>
    ///     <c>→</c> draws the post's next picture with its own description, the video between them skipped and not
    ///     counted, and the hint row says which of the three it is.
    /// </summary>
    [Fact]
    public async Task RightStepsToTheNextPictureWithItsOwnDescriptionSkippingTheVideo()
    {
        using var drawn = await Opened(ThreePicturesAndAVideo);

        drawn.Press(Key.CursorRight);

        Assert.Equal("m2", Showing(drawn));
        Assert.Contains(drawn.Rows(), row => row.Contains("The second sheep", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("The first sheep", StringComparison.Ordinal));
        Assert.Contains(drawn.Rows(), row => row.Contains("esc back · ←→ picture 2 of 3", StringComparison.Ordinal));
    }

    /// <summary>
    ///     <c>←</c> steps back the way <c>→</c> came, past the video again, to the first picture and its description.
    /// </summary>
    [Fact]
    public async Task LeftStepsBackToThePreviousPicture()
    {
        using var drawn = await Opened(ThreePicturesAndAVideo);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorLeft);

        Assert.Equal("m2", Showing(drawn));
        Assert.Contains(drawn.Rows(), row => row.Contains("The second sheep", StringComparison.Ordinal));
        Assert.Contains(drawn.Rows(), row => row.Contains("esc back · ←→ picture 2 of 3", StringComparison.Ordinal));

        drawn.Press(Key.CursorLeft);

        Assert.Equal("m1", Showing(drawn));
        Assert.Contains(drawn.Rows(), row => row.Contains("The first sheep", StringComparison.Ordinal));
        Assert.Contains(drawn.Rows(), row => row.Contains("esc back · ←→ picture 1 of 3", StringComparison.Ordinal));
    }

    /// <summary>
    ///     <c>←</c> on the first picture and <c>→</c> on the last change nothing on the screen: the steps stop at either
    ///     end rather than coming round.
    /// </summary>
    [Fact]
    public async Task TheStepsStopAtEitherEndRatherThanComingRound()
    {
        using var drawn = await Opened(ThreePicturesAndAVideo);

        var first = drawn.Rows();

        drawn.Press(Key.CursorLeft);

        Assert.Equal(first, drawn.Rows());
        Assert.Equal("m1", Showing(drawn));

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        var last = drawn.Rows();

        Assert.Contains(last, row => row.Contains("picture 3 of 3", StringComparison.Ordinal));

        drawn.Press(Key.CursorRight);

        Assert.Equal(last, drawn.Rows());
        Assert.Equal("m3", Showing(drawn));
    }

    /// <summary>
    ///     A post with one picture has nothing to step to, so neither the hint row nor the status row says the arrows or
    ///     counts it — though a video beside it is on the post.
    /// </summary>
    [Fact]
    public async Task OnePictureSaysNothingOfTheArrowsOrACount()
    {
        using var drawn = await Opened(APost.With(
            id: "110",
            media:
            [
                APost.APicture("m1", description: "The only sheep", shape: Wide),
                APost.Attached(MediaKind.Video, "v1", description: "A sheep running", shape: Wide),
            ]));

        var rows = drawn.Rows();

        Assert.Contains(rows, row => row.Contains("esc back", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains('←') || row.Contains('→'));
        Assert.DoesNotContain(rows, row => row.Contains("of 1", StringComparison.Ordinal));
    }

    /// <summary>And a post with several says the arrows on the status row as well as under the picture.</summary>
    [Fact]
    public async Task SeveralPicturesSayTheArrowsOnTheStatusRow()
    {
        using var drawn = await Opened(ThreePicturesAndAVideo);

        Assert.Contains("Picture: ←→", drawn.Rows()[^1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>esc</c> after stepping goes back at its first press, to the feed with the picture the screen was opened on
    ///     still walked — the second here, the walk having passed the video's label to reach it — rather than the one
    ///     stepped to.
    /// </summary>
    [Fact]
    public async Task EscAfterSteppingComesBackWithThePictureOpenedStillWalked()
    {
        using var drawn = await Reading(ThreePicturesAndAVideo);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        var walked = drawn.Rows();
        var reference = drawn.Shell.Screen.Reference;

        drawn.Press(Key.Enter);
        drawn.Settle();

        Assert.Equal("m2", Showing(drawn));

        drawn.Press(Key.CursorLeft);

        Assert.Equal("m1", Showing(drawn));

        drawn.Press(Key.Esc);

        Assert.Equal(1, drawn.Shell.Depth);
        Assert.NotNull(reference);
        Assert.Equal(reference, drawn.Shell.Screen.Reference);
        Assert.Equal(walked, drawn.Rows());
    }

    /// <summary>
    ///     The shell on an 80×30 terminal drawing sixel at 10×20 cells in the no-colour theme, reading
    ///     <paramref name="post" /> and one more under it, with every picture on it here.
    /// </summary>
    private static Task<DrawnShell> Reading(Post post) =>
        DrawnShell.Of(
            80,
            30,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(post, APost.With(id: "220")) },
            pictures: post.Media.Aggregate(
                new FakePictures(),
                (pictures, media) => pictures.Holding(media.Id, media.Shape!.Value.Width, media.Shape.Value.Height)),
            drawsPictures: true,
            raster: ARaster.Sixel());

    /// <summary><see cref="Reading" />, with the first picture on <paramref name="post" /> walked to and opened.</summary>
    private static async Task<DrawnShell> Opened(Post post)
    {
        var drawn = await Reading(post);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.Enter);
        drawn.Settle();

        return drawn;
    }

    /// <summary>Which picture the Picture screen's box is drawing.</summary>
    private static string? Showing(DrawnShell drawn) =>
        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible).PictureId;
}
