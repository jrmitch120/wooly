using System.Net;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;

namespace Wooly.Tests.Tui;

/// <summary>
///     Getting an attachment's pixels: decoded from what an instance actually serves, fetched once however often a
///     screen asks, and never allowed to become an error a reader has to deal with. Drawing them is ADR-0005's manual
///     smoke test; everything up to the moment they are handed over is here.
/// </summary>
public class PicturesTests
{
    [Fact]
    public void Decoder_ReadsAPngIntoPixels()
    {
        var picture = PictureDecoder.From(APng(4, 3));

        Assert.NotNull(picture);
        Assert.Equal(4, picture.Width);
        Assert.Equal(3, picture.Height);
    }

    /// <summary>Mastodon serves previews as JPEG as readily as PNG, so both have to read.</summary>
    [Fact]
    public void Decoder_ReadsAJpegIntoPixels()
    {
        var picture = PictureDecoder.From(AJpeg(8, 8));

        Assert.NotNull(picture);
        Assert.Equal(8, picture.Width);
        Assert.Equal(8, picture.Height);
    }

    /// <summary>
    ///     A picture is scaled down on the way in rather than at the view, and it keeps its proportions while it is:
    ///     what a terminal has room for is a few hundred pixels, and holding a photograph at full size is holding a
    ///     photograph.
    /// </summary>
    [Fact]
    public void Decoder_ScalesAPictureDownToWhatATerminalHasRoomFor()
    {
        var picture = PictureDecoder.From(APng(PictureDecoder.LongestSide * 4, PictureDecoder.LongestSide * 2));

        Assert.NotNull(picture);
        Assert.Equal(PictureDecoder.LongestSide, picture.Width);
        Assert.Equal(PictureDecoder.LongestSide / 2, picture.Height);
    }

    /// <summary>A picture already small enough is left as it is rather than stretched up to the limit.</summary>
    [Fact]
    public void Decoder_LeavesASmallPictureAlone()
    {
        var picture = PictureDecoder.From(APng(20, 10));

        Assert.NotNull(picture);
        Assert.Equal(20, picture.Width);
        Assert.Equal(10, picture.Height);
    }

    /// <summary>
    ///     A file that is not a picture — a truncated download, an error page served with a 200 — is nothing rather
    ///     than an exception. The post still says what is attached to it, which is what the reader had anyway.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("<!doctype html><title>Not found</title>")]
    public void Decoder_AnswersNothingForWhatIsNotAPicture(string content) =>
        Assert.Null(PictureDecoder.From(Encoding.UTF8.GetBytes(content)));

    /// <summary>
    ///     The whole reason this is asked on every draw: a screen redrawn on every keypress must cost one fetch, not
    ///     one a keypress.
    /// </summary>
    [Fact]
    public async Task Pictures_FetchesOneAttachmentOnce()
    {
        var asked = new List<string>();
        var landed = new TaskCompletionSource();

        using var pictures = new Pictures(
            (address, _) =>
            {
                asked.Add(address);

                return Task.FromResult<byte[]?>(APng(4, 4));
            },
            ADrawingTerminal,
            landed.SetResult);

        var media = APost.APicture();

        pictures.Want([Near(Drawn.Attached(media))]);

        await landed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.NotNull(pictures.Of(Drawn.Attached(media)));
        Assert.NotNull(pictures.Of(Drawn.Attached(media)));

        pictures.Want([Near(Drawn.Attached(media))]);

        Assert.Equal(media.Preview, Assert.Single(asked));
    }

    /// <summary>
    ///     The smaller copy is what gets fetched where the instance offered one: a terminal draws a few hundred pixels
    ///     across, and the original is somebody's data allowance.
    /// </summary>
    [Fact]
    public void Pictures_FetchesTheFileItselfOnlyWhereThereIsNoPreview()
    {
        var asked = new List<string>();

        using var pictures = new Pictures(
            (address, _) =>
            {
                asked.Add(address);

                return Task.FromResult<byte[]?>(null);
            },
            ADrawingTerminal,
            () => { });

        pictures.Want([Near(Drawn.Attached(APost.APicture() with { Preview = null }))]);

        Assert.Equal(APost.APicture().Url, Assert.Single(asked));
    }

    /// <summary>
    ///     A picture that cannot be had is not asked for again on the next frame, and is not an error anybody is shown:
    ///     nothing here throws, and the caller is told there is no picture, which is all it can act on.
    /// </summary>
    [Fact]
    public void Pictures_AsksOnceForAPictureItCannotHaveAndNeverThrows()
    {
        var asks = 0;

        using var pictures = new Pictures(
            (_, _) =>
            {
                asks++;

                throw new HttpRequestException("Connection refused");
            },
            ADrawingTerminal,
            () => { });

        pictures.Want([Near(Drawn.Attached(APost.APicture()))]);
        pictures.Want([Near(Drawn.Attached(APost.APicture()))]);
        pictures.Want([Near(Drawn.Attached(APost.APicture()))]);

        Assert.Null(pictures.Of(Drawn.Attached(APost.APicture())));
        Assert.Equal(1, asks);
    }

    /// <summary>Two attachments are two pictures, told apart by the instance's id for them rather than by their address.</summary>
    [Fact]
    public async Task Pictures_HoldsOnePictureForEachAttachment()
    {
        var landed = 0;
        var both = new TaskCompletionSource();

        using var pictures = new Pictures(
            (_, _) => Task.FromResult<byte[]?>(APng(4, 4)),
            ADrawingTerminal,
            () =>
            {
                if (Interlocked.Increment(ref landed) == 2)
                {
                    both.SetResult();
                }
            });

        pictures.Want([Near(Drawn.Attached(APost.APicture(id: "m1")))]);
        pictures.Want([Near(Drawn.Attached(APost.APicture(id: "m2")))]);

        await both.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.NotNull(pictures.Of(Drawn.Attached(APost.APicture(id: "m1"))));
        Assert.NotNull(pictures.Of(Drawn.Attached(APost.APicture(id: "m2"))));
    }

    /// <summary>
    ///     A morning's scrolling is not held in memory. Past what it has room for, the attachment wanted longest ago
    ///     is the one dropped, and the ones since are all still there.
    /// </summary>
    [Fact]
    public void Pictures_HoldsNoMoreThanItHasRoomFor()
    {
        var asks = 0;

        using var pictures = new Pictures(
            (_, _) =>
            {
                asks++;

                return Task.FromResult<byte[]?>(null);
            },
            ADrawingTerminal,
            () => { });

        // One more than there is room for, which drops the first.
        for (var at = 0; at <= Pictures.MostHeld; at++)
        {
            pictures.Want([Near(Drawn.Attached(APost.APicture(id: $"m{at}")))]);
        }

        Assert.Equal(Pictures.MostHeld + 1, asks);

        // The most recent is still remembered, so wanting it again sends for nothing.
        pictures.Want([Near(Drawn.Attached(APost.APicture(id: $"m{Pictures.MostHeld}")))]);

        Assert.Equal(Pictures.MostHeld + 1, asks);

        // The first is gone, so wanting it again sends for it again — which is what having dropped it means.
        pictures.Want([Near(Drawn.Attached(APost.APicture(id: "m0")))]);

        Assert.Equal(Pictures.MostHeld + 2, asks);
    }

    /// <summary>
    ///     A picture dropped to make room is said to be dropped, by id, so that a terminal holding a copy of it can be
    ///     told to let go of it too (#292).
    /// </summary>
    [Fact]
    public void Pictures_SaysWhichPictureItDropped()
    {
        var dropped = new List<string>();

        using var pictures = new Pictures(
            (_, _) => Task.FromResult<byte[]?>(null),
            ADrawingTerminal,
            () => { },
            dropped.Add);

        for (var at = 0; at < Pictures.MostHeld; at++)
        {
            pictures.Want([Near(Drawn.Attached(APost.APicture(id: $"m{at}")))]);
        }

        Assert.Empty(dropped);

        pictures.Want([Near(Drawn.Attached(APost.APicture(id: $"m{Pictures.MostHeld}")))]);

        Assert.Equal(["m0"], dropped);
    }

    /// <summary>
    ///     Wanting a picture again is what keeps it: the one let go of is the one wanted longest ago, not the one
    ///     fetched longest ago. Going by the fetch is how a picture still on screen came to be dropped mid-scroll, and
    ///     sent for again a frame later (#345).
    /// </summary>
    [Fact]
    public void Pictures_LetsGoOfThePictureWantedLongestAgo()
    {
        var dropped = new List<string>();

        using var pictures = new Pictures(
            (_, _) => Task.FromResult<byte[]?>(null),
            ADrawingTerminal,
            () => { },
            dropped.Add);

        for (var at = 0; at < Pictures.MostHeld; at++)
        {
            pictures.Want([Near(Drawn.Attached(APost.APicture(id: $"m{at}")))]);
        }

        // The first fetched, wanted again: it is now the most recently wanted, and the second is the oldest.
        pictures.Want([Near(Drawn.Attached(APost.APicture(id: "m0")))]);
        pictures.Want([Near(Drawn.Attached(APost.APicture(id: "new")))]);

        Assert.Equal(["m1"], dropped);
    }

    /// <summary>
    ///     What is on screen in the latest frame is never let go of, however far over its room that leaves the cache.
    ///     Once it is off screen it is ordinary again, and the farthest of what a frame wanted goes first.
    /// </summary>
    [Fact]
    public void Pictures_NeverLetsGoOfWhatIsOnScreen()
    {
        var asked = new List<string>();
        var dropped = new List<string>();

        using var pictures = new Pictures(
            (address, _) =>
            {
                asked.Add(address);

                return Task.FromResult<byte[]?>(null);
            },
            ADrawingTerminal,
            () => { },
            dropped.Add);

        var screenful = Enumerable.Range(0, Pictures.MostHeld + 3)
                                  .Select(at => Drawn.Attached(APost.APicture(id: $"s{at}")))
                                  .ToList();

        pictures.Want([.. screenful.Select(OnScreen)]);

        Assert.Equal(Pictures.MostHeld + 3, asked.Count);
        Assert.Empty(dropped);

        // The page moves on: the next frame has none of them on screen, so the cache comes back to its room, letting
        // go of the four of them farthest from where the screen was.
        pictures.Want([Near(Drawn.Attached(APost.APicture(id: "next")))]);

        Assert.Equal(["s34", "s33", "s32", "s31"], dropped);
    }

    /// <summary>
    ///     A frame wanting more than there is room for is sent for nearest first and only as far as the room goes.
    ///     Sending for the rest would only have them let go of again at once, and sent for again on the next frame,
    ///     which is a fetch per keypress.
    /// </summary>
    [Fact]
    public void Pictures_SendsForNoMoreOfAFrameThanItHasRoomFor()
    {
        var asked = new List<string>();
        var dropped = new List<string>();

        using var pictures = new Pictures(
            (address, _) =>
            {
                asked.Add(address);

                return Task.FromResult<byte[]?>(null);
            },
            ADrawingTerminal,
            () => { },
            dropped.Add);

        var frame = Enumerable.Range(0, Pictures.MostHeld + 3)
                              .Select(at => Near(Drawn.Attached(APost.APicture(id: $"m{at}"))))
                              .ToList();

        pictures.Want(frame);
        pictures.Want(frame);

        Assert.Equal(Pictures.MostHeld, asked.Count);
        Assert.Empty(dropped);
    }

    /// <summary>
    ///     Fetches start nearest the screen first, whatever order the pictures were first wanted in: a picture wanted
    ///     far ahead and still waiting for its turn goes after one the page has since come near. Never more than
    ///     <see cref="Pictures.AtATime" /> at once while they do.
    /// </summary>
    [Fact]
    public async Task Pictures_StartsTheNearestFetchFirst()
    {
        var fetch = new Gated();

        using var pictures = new Pictures(fetch.Fetch, ADrawingTerminal, () => { });

        var busy = Busy();

        pictures.Want([.. busy, Near(Picture("far"))]);
        pictures.Want([.. busy, Near(Picture("near")), Near(Picture("far"))]);

        Assert.Equal(Pictures.AtATime, fetch.Asked.Count);

        fetch.Answer("b0");
        await fetch.Until(Pictures.AtATime + 1);

        fetch.Answer("b1");
        await fetch.Until(Pictures.AtATime + 2);

        Assert.Equal(["near", "far"], fetch.Asked.Skip(Pictures.AtATime));
    }

    /// <summary>
    ///     A picture still waiting for its turn when a frame stops wanting it is never fetched: the page has left it
    ///     behind, and fetching it would hold up what the page is now near. Wanted again later, it is sent for then.
    /// </summary>
    [Fact]
    public async Task Pictures_AbandonsAQueuedFetchNoLongerWanted()
    {
        var fetch = new Gated();

        using var pictures = new Pictures(fetch.Fetch, ADrawingTerminal, () => { });

        var busy = Busy();

        pictures.Want([.. busy, Near(Picture("left behind"))]);
        pictures.Want([.. busy, Near(Picture("ahead"))]);

        fetch.Answer("b0");
        await fetch.Until(Pictures.AtATime + 1);

        fetch.Answer("b1");
        fetch.Answer("b2");
        fetch.Answer("b3");
        fetch.Answer("ahead");

        pictures.Want([Near(Picture("left behind"))]);
        await fetch.Until(Pictures.AtATime + 2);

        Assert.Equal(["ahead", "left behind"], fetch.Asked.Skip(Pictures.AtATime));
    }

    /// <summary>
    ///     A fetch already under way when its picture stops being wanted is let finish, and what it brings is held:
    ///     the bytes are already on their way, and the reader may yet turn round.
    /// </summary>
    [Fact]
    public async Task Pictures_HoldsWhatAnInFlightFetchBringsAfterItIsNoLongerWanted()
    {
        var fetch = new Gated();
        var landed = new TaskCompletionSource();

        using var pictures = new Pictures(fetch.Fetch, ADrawingTerminal, landed.SetResult);

        pictures.Want([Near(Picture("leaving"))]);
        pictures.Want([Near(Picture("elsewhere"))]);

        fetch.Answer("leaving", APng(4, 4));
        await landed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.NotNull(pictures.Of(Picture("leaving")));
    }

    /// <summary>
    ///     The adapter over <see cref="HttpClient" />, tested at the one seam under it (ADR-0005): what a file server
    ///     answers is what gets decoded.
    /// </summary>
    [Fact]
    public async Task Over_FetchesAPreviewAndDecodesWhatCameBack()
    {
        var network = new ScriptedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(APng(6, 4)),
        });

        var landed = new TaskCompletionSource();

        using var http = new HttpClient(network);
        using var pictures = Pictures.Over(http, ADrawingTerminal, landed.SetResult);

        pictures.Want([Near(Drawn.Attached(APost.APicture()))]);

        await landed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var picture = pictures.Of(Drawn.Attached(APost.APicture()));

        Assert.NotNull(picture);
        Assert.Equal(6, picture.Width);
        Assert.Equal(APost.APicture().Preview, Assert.Single(network.Requests).RequestUri?.ToString());
    }

    /// <summary>A file server that answers with anything but a picture is answered with no picture, not an exception.</summary>
    [Fact]
    public void Over_TakesARefusalAsNoPicture()
    {
        var network = new ScriptedHttpMessageHandler(ScriptedHttpMessageHandler.Status(HttpStatusCode.NotFound));

        using var http = new HttpClient(network);
        using var pictures = Pictures.Over(http, ADrawingTerminal, () => { });

        pictures.Want([Near(Drawn.Attached(APost.APicture()))]);

        Assert.Null(pictures.Of(Drawn.Attached(APost.APicture())));
    }

    /// <summary>
    ///     A body larger than any preview could be is refused, and refused by counting the bytes rather than by
    ///     believing the header — a server that declares no length, or declares one and sends another, would otherwise
    ///     be deciding how much of this client's memory to use.
    /// </summary>
    [Fact]
    public async Task Over_RefusesABodyLargerThanAnyPreviewCouldBe()
    {
        var landed = false;

        // Chunked: no Content-Length at all, so the only way to know it is too big is to have counted.
        var network = new ScriptedHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(new byte[Pictures.MostBytes + 1])),
        });

        using var http = new HttpClient(network);
        using var pictures = Pictures.Over(http, ADrawingTerminal, () => landed = true);

        pictures.Want([Near(Drawn.Attached(APost.APicture()))]);

        // Nothing announces a refusal, so the wait is for the request to have been made and answered.
        while (network.Requests.Count == 0)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Null(pictures.Of(Drawn.Attached(APost.APicture())));
        Assert.False(landed);
    }

    /// <summary>An attachment's picture, by the id the instance gave it — which is also the address it is fetched from.</summary>
    private static Drawn Picture(string id) => new(id, id);

    /// <summary>As many pictures as are fetched at once, named <c>b0</c> on, to keep every fetch busy.</summary>
    private static List<WantedPicture> Busy() =>
        [.. Enumerable.Range(0, Pictures.AtATime).Select(at => Near(Picture($"b{at}")))];

    /// <summary>A picture a frame wants that is near the screen but not on it.</summary>
    private static WantedPicture Near(Drawn drawn) => new(drawn, OnScreen: false);

    /// <summary>A picture a frame wants that is on screen in it.</summary>
    private static WantedPicture OnScreen(Drawn drawn) => new(drawn, OnScreen: true);

    /// <summary>A terminal that draws pictures, since none of these tests is about one that does not.</summary>
    private static CellSize? ADrawingTerminal() => new CellSize(10, 20);

    private static byte[] APng(int width, int height) => Encoded(width, height, new PngEncoder());

    private static byte[] AJpeg(int width, int height) => Encoded(width, height, new JpegEncoder());

    /// <summary>
    ///     A real file in a real format, rather than a byte array a test made up: what is being proved is that what an
    ///     instance serves decodes, and only an actual encoder can produce that.
    /// </summary>
    private static byte[] Encoded(int width, int height, IImageEncoder encoder)
    {
        using var image = new Image<Rgba32>(width, height);
        using var bytes = new MemoryStream();

        image.Save(bytes, encoder);

        return bytes.ToArray();
    }

    /// <summary>
    ///     A file server that answers only when told to, so that a test can hold fetches open and see which are
    ///     started, and in what order, while they are.
    /// </summary>
    private sealed class Gated
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, TaskCompletionSource<byte[]?>> _answers = [];
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

        public Task<byte[]?> Fetch(string address, CancellationToken cancellation)
        {
            lock (_gate)
            {
                _asked.Add(address);

                return Answering(address).Task;
            }
        }

        /// <summary>Lets the fetch of <paramref name="address" /> finish, with <paramref name="bytes" /> or nothing.</summary>
        public void Answer(string address, byte[]? bytes = null)
        {
            lock (_gate)
            {
                Answering(address).TrySetResult(bytes);
            }
        }

        /// <summary>Waits for <paramref name="count" /> fetches to have started.</summary>
        public async Task Until(int count)
        {
            var giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(5);

            while (Asked.Count < count && DateTime.UtcNow < giveUp)
            {
                await Task.Delay(5, TestContext.Current.CancellationToken);
            }
        }

        private TaskCompletionSource<byte[]?> Answering(string address)
        {
            if (!_answers.TryGetValue(address, out var answer))
            {
                answer = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _answers[address] = answer;
            }

            return answer;
        }
    }
}
