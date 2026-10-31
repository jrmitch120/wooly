using Terminal.Gui.Input;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>←</c> and <c>→</c> on a <b>Picture screen</b> step to the post's previous and next drawn still picture, in the
///     order they are drawn, stopping at either end; the crumb says which of how many is showing, and <c>esc</c> comes
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
    ///     counted, and the crumb says which of the three it is.
    /// </summary>
    [Fact]
    public async Task RightStepsToTheNextPictureWithItsOwnDescriptionSkippingTheVideo()
    {
        using var drawn = await Opened(ThreePicturesAndAVideo);

        drawn.Press(Key.CursorRight);

        Assert.Equal(Original("m2"), Showing(drawn));
        Assert.Contains(drawn.Rows(), row => row.Contains("The second sheep", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn.Rows(), row => row.Contains("The first sheep", StringComparison.Ordinal));
        Assert.Equal("Picture 2 of 3 by @jeff@mastodon.social", Crumb(drawn));
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

        Assert.Equal(Original("m2"), Showing(drawn));
        Assert.Contains(drawn.Rows(), row => row.Contains("The second sheep", StringComparison.Ordinal));
        Assert.Equal("Picture 2 of 3 by @jeff@mastodon.social", Crumb(drawn));

        drawn.Press(Key.CursorLeft);

        Assert.Equal(Original("m1"), Showing(drawn));
        Assert.Contains(drawn.Rows(), row => row.Contains("The first sheep", StringComparison.Ordinal));
        Assert.Equal("Picture 1 of 3 by @jeff@mastodon.social", Crumb(drawn));
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
        Assert.Equal(Original("m1"), Showing(drawn));

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorRight);

        var last = drawn.Rows();

        Assert.Equal("Picture 3 of 3 by @jeff@mastodon.social", Crumb(drawn));

        drawn.Press(Key.CursorRight);

        Assert.Equal(last, drawn.Rows());
        Assert.Equal(Original("m3"), Showing(drawn));
    }

    /// <summary>
    ///     A post with one picture has nothing to step to, so neither the crumb nor the status row says the arrows or
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

        Assert.Equal("Picture by @jeff@mastodon.social", Crumb(drawn));
        Assert.DoesNotContain(rows, row => row.Contains('←') || row.Contains('→'));
        Assert.DoesNotContain(rows, row => row.Contains("of 1", StringComparison.Ordinal));
    }

    /// <summary>
    ///     And a post with several says the arrows on the status row, and nowhere in the content region: the row under
    ///     the picture that said the keys again is gone, its rows given to the picture.
    /// </summary>
    [Fact]
    public async Task SeveralPicturesSayTheArrowsOnTheStatusRowAlone()
    {
        using var drawn = await Opened(ThreePicturesAndAVideo);

        var rows = drawn.Rows();

        Assert.Contains("Picture: ←→", rows[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(rows[..^1], row => row.Contains('←') || row.Contains("esc back", StringComparison.Ordinal));
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

        Assert.Equal(Original("m2"), Showing(drawn));

        drawn.Press(Key.CursorLeft);

        Assert.Equal(Original("m1"), Showing(drawn));

        drawn.Press(Key.Esc);

        Assert.Equal(1, drawn.Shell.Depth);
        Assert.NotNull(reference);
        Assert.Equal(reference, drawn.Shell.Screen.Reference);
        Assert.Equal(walked, drawn.Rows());
    }

    /// <summary>
    ///     A step to a picture whose original landed while the one before it was shown draws that original at once,
    ///     with no Stand-in first: the screen sent for every picture on the post, not only the one showing.
    /// </summary>
    [Fact]
    public async Task AStepToAPictureWhoseOriginalHasLandedDrawsItWithNoStandIn()
    {
        using var pictures = Served();
        using var drawn = await Opened(TwoBlurredPictures, pictures);

        await Until(drawn, () => pictures.Of(Drawn.Original(APost.APicture("m2"), 0, 0)) is not null);

        drawn.Press(Key.CursorRight);

        Assert.Equal(Original("m2"), Showing(drawn));
    }

    /// <summary>
    ///     A step back to a picture stepped off finds its original still held, and draws it at once: stepping off a
    ///     picture no longer lets its original go, only the screen going does.
    /// </summary>
    [Fact]
    public async Task AStepBackToAPictureSteppedOffFindsItStillDrawn()
    {
        using var pictures = Served();
        using var drawn = await Opened(TwoBlurredPictures, pictures);

        await Until(drawn, () => pictures.Of(Drawn.Original(APost.APicture("m2"), 0, 0)) is not null);

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.CursorLeft);

        Assert.Equal(Original("m1"), Showing(drawn));
    }

    /// <summary>
    ///     On a Kitty terminal drawing placeholders, a landed original not showing is encoded ahead, in the box it would
    ///     be drawn in, so that a step to it sends it on the very frame of the step rather than leaving its box blank
    ///     until it has been encoded.
    /// </summary>
    [Fact]
    public async Task AStepSendsALandedOriginalOnTheFrameOfTheStepOnAKittyTerminal()
    {
        var encoding = new List<Action>();
        var images = new FakeTerminalImages();
        var post = TwoBlurredPictures;

        using var drawn = await DrawnShell.Of(
            80,
            30,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(post, APost.With(id: "220")) },
            pictures: post.Media.Aggregate(
                new FakePictures(),
                (pictures, media) => pictures
                    .Holding(media.Id, 400, 300)
                    .HoldingOriginal(media.Id, 400, 300)),
            kittyImages: images,
            drawsPlaceholders: true,
            encoding: encoding.Add,
            raster: ARaster.Placeholders());

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.Enter);
        drawn.Settle();

        while (encoding.Count > 0)
        {
            Action[] waiting = [.. encoding];

            encoding.Clear();
            Array.ForEach(waiting, encode => encode());
            drawn.Redraw();
        }

        var sent = images.Transmitted.Count;

        drawn.Press(Key.CursorRight);

        Assert.Equal(sent + 1, images.Transmitted.Count);
        Assert.Empty(encoding);
    }

    /// <summary>
    ///     Leaving the Picture screen lets go of the original of every picture on the post, the one showing and the
    ///     one it held for a step alike (the smaller copies staying held is the cache's own to show, in
    ///     <see cref="PicturesTests" />).
    /// </summary>
    [Fact]
    public async Task LeavingThePictureScreenLetsGoOfEveryOriginalItHeld()
    {
        using var pictures = Served();
        using var drawn = await Opened(TwoBlurredPictures, pictures);

        await Until(drawn, () => pictures.Of(Drawn.Original(APost.APicture("m2"), 0, 0)) is not null);

        drawn.Press(Key.CursorRight);

        Assert.NotNull(pictures.Of(Drawn.Original(APost.APicture("m1"), 0, 0)));

        drawn.Press(Key.Esc);

        Assert.Null(pictures.Of(Drawn.Original(APost.APicture("m1"), 0, 0)));
        Assert.Null(pictures.Of(Drawn.Original(APost.APicture("m2"), 0, 0)));
    }

    /// <summary>
    ///     The original of the picture showing is sent for first, and those of the post's other pictures only once it
    ///     has landed, so that the picture being looked at is not kept waiting on the ones that are not.
    /// </summary>
    [Fact]
    public async Task TheOriginalShowingIsSentForBeforeTheOthers()
    {
        var server = new Gated();

        using var pictures = new Pictures(server.Fetch, () => { });
        using var drawn = await Opened(TwoBlurredPictures, pictures);

        drawn.Redraw();

        Assert.Contains(OriginalAddress("m1"), server.Asked);
        Assert.DoesNotContain(OriginalAddress("m2"), server.Asked);

        server.Answer(OriginalAddress("m1"), APng(400, 300));

        await Until(drawn, () => server.Asked.Contains(OriginalAddress("m2")));
    }

    /// <summary>
    ///     The shell on an 80×30 terminal drawing sixel at 10×20 cells in the no-colour theme, reading
    ///     <paramref name="post" /> and one more under it, with every picture on it here, its smaller copy and its
    ///     original alike.
    /// </summary>
    private static Task<DrawnShell> Reading(Post post) =>
        DrawnShell.Of(
            80,
            30,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(post, APost.With(id: "220")) },
            pictures: post.Media.Aggregate(
                new FakePictures(),
                (pictures, media) => pictures
                    .Holding(media.Id, media.Shape!.Value.Width, media.Shape.Value.Height)
                    .HoldingOriginal(media.Id, media.Shape.Value.Width, media.Shape.Value.Height)),
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

    /// <summary>Two pictures, each with a blur for its Stand-in, so that a Stand-in drawn is a box drawn.</summary>
    private static Post TwoBlurredPictures => APost.With(
        id: "110",
        media:
        [
            APost.APicture("m1", description: "The first sheep", shape: Wide, blurhash: StandInBlurTests.AHash),
            APost.APicture("m2", description: "The second sheep", shape: Wide, blurhash: StandInBlurTests.AHash),
        ]);

    /// <summary>
    ///     The shell as <see cref="Reading" /> draws it, its pictures fetched by <paramref name="pictures" /> rather than
    ///     said outright, and the first picture on <paramref name="post" /> walked to and opened.
    /// </summary>
    private static async Task<DrawnShell> Opened(Post post, Pictures pictures)
    {
        var drawn = await DrawnShell.Of(
            80,
            30,
            Themes.Plain,
            new AShell { Timelines = FakeTimelineReader.Holding(post, APost.With(id: "220")) },
            pictures: pictures,
            drawsPictures: true,
            raster: ARaster.Sixel());

        drawn.Press(Key.CursorRight);
        drawn.Press(Key.Enter);
        drawn.Settle();

        return drawn;
    }

    /// <summary>
    ///     The real cache, over a file server that answers every address at once with a picture — so that what is held
    ///     and what is let go of is the cache's own doing, and only what a frame wants is ever sent for.
    /// </summary>
    private static Pictures Served() =>
        new((_, _, _) => Task.FromResult(Fetched.Of(APng(400, 300))), () => { });

    /// <summary>
    ///     <paramref name="drawn" /> drawn again and again, as the client draws whenever a picture lands, until
    ///     <paramref name="landed" /> says what the test waits for has.
    /// </summary>
    private static async Task Until(DrawnShell drawn, Func<bool> landed)
    {
        var giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (true)
        {
            drawn.Redraw();

            if (landed())
            {
                return;
            }

            Assert.True(DateTime.UtcNow < giveUp, "what was waited for never landed");

            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Where the original of the attachment <paramref name="id" /> names is fetched from.</summary>
    private static string OriginalAddress(string id) => APost.APicture(id).Url;

    /// <summary>
    ///     A file server that answers an address only once told to, so that a test can see what was sent for before
    ///     anything landed.
    /// </summary>
    private sealed class Gated
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, TaskCompletionSource<Fetched>> _answers = [];
        private readonly List<string> _asked = [];

        /// <summary>Every address asked for, in the order the fetches started.</summary>
        public List<string> Asked
        {
            get
            {
                lock (_gate)
                {
                    return [.. _asked];
                }
            }
        }

        public Task<Fetched> Fetch(string address, int most, CancellationToken cancellation)
        {
            lock (_gate)
            {
                _asked.Add(address);

                return Answering(address).Task;
            }
        }

        /// <summary>Lets the fetch of <paramref name="address" /> finish with <paramref name="bytes" />.</summary>
        public void Answer(string address, byte[] bytes)
        {
            lock (_gate)
            {
                Answering(address).TrySetResult(Fetched.Of(bytes));
            }
        }

        private TaskCompletionSource<Fetched> Answering(string address)
        {
            if (!_answers.TryGetValue(address, out var answer))
            {
                answer = new TaskCompletionSource<Fetched>(TaskCreationOptions.RunContinuationsAsynchronously);
                _answers[address] = answer;
            }

            return answer;
        }
    }

    /// <summary>A PNG of <paramref name="width" /> by <paramref name="height" />, as a file server sends one.</summary>
    private static byte[] APng(int width, int height)
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        using var bytes = new MemoryStream();

        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, bytes);

        return bytes.ToArray();
    }

    /// <summary>The crumb the Picture screen says it is, the last on the stack.</summary>
    private static string Crumb(DrawnShell drawn) => drawn.Shell.Crumbs[^1];

    /// <summary>The id the original of the attachment <paramref name="id" /> names is drawn under.</summary>
    private static string Original(string id) => Drawn.Original(APost.APicture(id), 0, 0).Id;

    /// <summary>Which picture the Picture screen's box is drawing.</summary>
    private static string? Showing(DrawnShell drawn) =>
        Assert.Single(drawn.Content.SubViews.OfType<PictureView>(), view => view.Visible).PictureId;
}
