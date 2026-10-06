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

        pictures.Want([Near(Drawn.Attached(APost.APicture(id: "m1"))), Near(Drawn.Attached(APost.APicture(id: "m2")))]);

        await both.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.NotNull(pictures.Of(Drawn.Attached(APost.APicture(id: "m1"))));
        Assert.NotNull(pictures.Of(Drawn.Attached(APost.APicture(id: "m2"))));
    }

    /// <summary>
    ///     A file is held to a budget in bytes too, past which the file wanted longest ago is let go of. A picture whose
    ///     pixels and file have both gone is fetched again when it is next wanted, which is what having let go of it
    ///     means — and is the only way a picture once fetched is fetched twice (ADR-0025).
    /// </summary>
    [Fact]
    public async Task Pictures_FetchesAPictureAgainOnlyOnceItsFileIsLetGoOfToo()
    {
        var asked = new List<string>();
        var dropped = new List<string>();
        var photograph = ANoisyPng(1000, 640);

        using var pictures = APictures(_ => photograph, out var landings, asked: asked, dropped: dropped);

        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));

        for (var at = 0; at <= fitting; at++)
        {
            pictures.Want([Near(APicture($"m{at}"))]);
            await landings.Landed(at + 1);
        }

        // Its pixels went to make room in the decoded tier, and its file long before, since files of this size fill
        // the encoded tier's budget in fewer pictures than pixels fill the decoded tier's.
        Assert.Equal(["m0"], dropped);
        Assert.True(photograph.Length * (fitting + 1L) > Pictures.EncodedBudget);

        pictures.Want([Near(APicture("m0"))]);
        await landings.Landed(fitting + 2);

        Assert.Equal(fitting + 2, asked.Count);
    }

    /// <summary>
    ///     Wanting a picture again is what keeps it: the one let go of is the one wanted longest ago, not the one
    ///     fetched longest ago. Going by the fetch is how a picture still on screen came to be dropped mid-scroll, and
    ///     sent for again a frame later (#345).
    /// </summary>
    [Fact]
    public async Task Pictures_LetsGoOfThePictureWantedLongestAgo()
    {
        var dropped = new List<string>();

        using var pictures = APictures(_ => APng(1000, 640), out var landings, dropped: dropped);

        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));

        for (var at = 0; at < fitting; at++)
        {
            pictures.Want([Near(APicture($"m{at}"))]);
            await landings.Landed(at + 1);
        }

        // The first fetched, wanted again: it is now the most recently wanted, and the second is the oldest.
        pictures.Want([Near(APicture("m0"))]);
        pictures.Want([Near(APicture("new"))]);
        await landings.Landed(fitting + 1);

        Assert.Equal(["m1"], dropped);
    }

    /// <summary>
    ///     What is on screen in the latest frame is never let go of, however far over its budget that leaves the
    ///     decoded tier. Once it is off screen it is ordinary again, and the farthest of what a frame wanted goes first.
    /// </summary>
    [Fact]
    public async Task Pictures_NeverLetsGoOfWhatIsOnScreen()
    {
        var dropped = new List<string>();

        using var pictures = APictures(_ => APng(1000, 640), out var landings, dropped: dropped);

        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));
        var screenful = Enumerable.Range(0, fitting + 3).Select(at => APicture($"s{at}")).ToList();

        pictures.Want([.. screenful.Select(OnScreen)]);
        await landings.Landed(fitting + 3);

        Assert.Empty(dropped);
        Assert.All(screenful, drawn => Assert.NotNull(pictures.Of(drawn)));

        // The page moves on: the next frame has none of them on screen, so the tier comes back to its budget, letting
        // go of the four of them farthest from where the screen was — three for the screenful, one for what is next.
        pictures.Want([Near(APicture("next"))]);
        await landings.Landed(fitting + 4);

        Assert.Equal([$"s{fitting + 2}", $"s{fitting + 1}", $"s{fitting}", $"s{fitting - 1}"], dropped);
    }

    /// <summary>
    ///     A frame wanting more pixels than there is room for decodes, once their sizes are known, only as many as
    ///     there is room for, nearest first. Decoding the rest would only let go of them again at once, and decode them
    ///     again on the next frame, which is a decode per keypress.
    /// </summary>
    [Fact]
    public async Task Pictures_DecodesNoMoreOfAFrameThanItHasRoomFor()
    {
        var asked = new List<string>();
        var dropped = new List<string>();

        using var pictures = APictures(_ => APng(1000, 640), out var landings, asked: asked, dropped: dropped);

        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));
        var frame = Enumerable.Range(0, fitting + 3).Select(at => Near(APicture($"m{at}"))).ToList();

        // Nothing's size is known before it is fetched, so the first frame finds out by decoding the lot.
        pictures.Want(frame);
        await landings.Landed(fitting + 3);

        Assert.Equal(3, dropped.Count);

        pictures.Want(frame);
        pictures.Want(frame);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(fitting + 3, landings.Count);
        Assert.Equal(fitting + 3, asked.Count);
        Assert.Equal(3, dropped.Count);
    }

    /// <summary>
    ///     A window made wider can draw a picture larger than it was decoded for, so it is decoded again at the new
    ///     size from the file already held, and lands again to be drawn sharp — with no fetch (ADR-0025).
    /// </summary>
    [Fact]
    public async Task Pictures_DecodesAPictureAgainWhenTheWindowGrowsWithoutFetching()
    {
        var asked = new List<string>();
        var width = 50;

        using var pictures = APictures(_ => APng(4000, 1000), out var landings, columns: () => width, asked: asked);

        pictures.Want([OnScreen(APicture("m"))]);
        await landings.Landed(1);

        Assert.Equal(500, pictures.Of(APicture("m"))?.Width);

        width = 100;
        pictures.Want([OnScreen(APicture("m"))]);
        await landings.Landed(2);

        Assert.Equal(1000, pictures.Of(APicture("m"))?.Width);
        Assert.Single(asked);

        // Narrower again: the sharper pixels are drawn scaled down rather than decoded a third time.
        width = 50;
        pictures.Want([OnScreen(APicture("m"))]);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(2, landings.Count);
        Assert.Equal(1000, pictures.Of(APicture("m"))?.Width);
    }

    /// <summary>
    ///     A fetch still on its way when the page moved on is not wasted: it lands as a file, undecoded, since no frame
    ///     has wanted its pixels since. Scrolling back to it is then a decode rather than a second fetch (ADR-0025).
    /// </summary>
    [Fact]
    public async Task Pictures_KeepsTheFileOfAFetchThatLandsAfterThePageMovedOn()
    {
        var asked = new List<string>();
        var slow = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var landings = new Landings();

        using var pictures = new Pictures(
            (address, _) =>
            {
                lock (asked)
                {
                    asked.Add(address);
                }

                return address == APicture("slow").Address ? slow.Task : Task.FromResult<byte[]?>(APng(4, 4));
            },
            ADrawingTerminal,
            landings.Land,
            columns: () => 100);

        pictures.Want([Near(APicture("slow"))]);
        pictures.Want([Near(APicture("other"))]);
        await landings.Landed(1);

        slow.SetResult(APng(4, 4));
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(1, landings.Count);
        Assert.Null(pictures.Of(APicture("slow")));

        pictures.Want([Near(APicture("slow"))]);
        await landings.Landed(2);

        Assert.NotNull(pictures.Of(APicture("slow")));
        Assert.Equal(2, asked.Count);
    }

    /// <summary>
    ///     A picture is held at no more than the largest box this window could draw it in: the full width of the
    ///     window by the post screen's row cap, in pixels (ADR-0025). A hundred columns of ten-pixel cells is a thousand
    ///     pixels across, so a photograph four thousand across is held at a thousand, in its own proportions.
    /// </summary>
    [Fact]
    public async Task Pictures_HoldsAPictureAtTheLargestBoxThisWindowCouldDraw()
    {
        using var pictures = APictures(_ => APng(4000, 1000), out var landings, columns: () => 100);

        pictures.Want([Near(Drawn.Attached(APost.APicture()))]);

        await landings.Landed(1);

        var picture = pictures.Of(Drawn.Attached(APost.APicture()));

        Assert.NotNull(picture);
        Assert.Equal(1000, picture.Width);
        Assert.Equal(250, picture.Height);
    }

    /// <summary>
    ///     An avatar is never drawn larger than an account screen's header box, eight cells by four, so that is all it
    ///     is held at: an avatar costs kilobytes, not the hundreds an instance's 400-pixel square would decode to.
    /// </summary>
    [Fact]
    public async Task Pictures_HoldsAnAvatarAtItsOwnBox()
    {
        using var pictures = APictures(_ => APng(400, 400), out var landings);

        pictures.Want([Near(Drawn.Avatar("alice@example.social", "https://files.example/alice.png"))]);

        await landings.Landed(1);

        var picture = pictures.Of(Drawn.Avatar("alice@example.social", "https://files.example/alice.png"));

        Assert.NotNull(picture);
        Assert.Equal(80, picture.Width);
        Assert.Equal(80, picture.Height);
    }

    /// <summary>
    ///     Pixels are held to a budget in bytes, not a count of pictures: past it the picture wanted longest ago is let
    ///     go of, and said to be. Its downloaded bytes are still held, so wanting it again decodes it again from those,
    ///     and the network is not asked twice (ADR-0025).
    /// </summary>
    [Fact]
    public async Task Pictures_DecodesAPictureLetGoOfAgainFromItsBytesWithoutFetching()
    {
        var asked = new List<string>();
        var dropped = new List<string>();

        using var pictures = APictures(_ => APng(1000, 640), out var landings, asked: asked, dropped: dropped);

        // Each a thousand by 640 at four bytes a pixel, which is the largest a hundred-column window draws them.
        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));

        for (var at = 0; at < fitting; at++)
        {
            pictures.Want([Near(APicture($"m{at}"))]);
            await landings.Landed(at + 1);
        }

        Assert.Empty(dropped);

        pictures.Want([Near(APicture("over"))]);
        await landings.Landed(fitting + 1);

        Assert.Equal(["m0"], dropped);
        Assert.Null(pictures.Of(APicture("m0")));

        pictures.Want([Near(APicture("m0"))]);
        await landings.Landed(fitting + 2);

        Assert.NotNull(pictures.Of(APicture("m0")));
        Assert.Equal(fitting + 1, asked.Count);
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

    /// <summary>
    ///     A cache over a fetch that answers with <paramref name="serve" />'s bytes for each address, writing down every
    ///     address it is asked for in <paramref name="asked" /> and every picture let go of in <paramref name="dropped" />.
    /// </summary>
    private static Pictures APictures(
        Func<string, byte[]?> serve,
        out Landings landings,
        Func<int>? columns = null,
        Func<CellSize?>? cell = null,
        List<string>? asked = null,
        List<string>? dropped = null)
    {
        var landed = new Landings();

        landings = landed;

        return new Pictures(
            (address, _) =>
            {
                lock (landed)
                {
                    asked?.Add(address);
                }

                return Task.FromResult(serve(address));
            },
            cell ?? ADrawingTerminal,
            landed.Land,
            id =>
            {
                lock (landed)
                {
                    dropped?.Add(id);
                }
            },
            columns ?? (() => 100));
    }

    /// <summary>How many times a cache has said a picture landed, and a way to wait for the next of them.</summary>
    private sealed class Landings
    {
        private int _landed;

        public int Count => Volatile.Read(ref _landed);

        public void Land() => Interlocked.Increment(ref _landed);

        /// <summary>Waits until <paramref name="count" /> pictures in all have landed.</summary>
        public async Task Landed(int count)
        {
            var giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(10);

            while (Count < count)
            {
                Assert.True(DateTime.UtcNow < giveUp, $"{Count} of {count} pictures landed");

                await Task.Delay(5, TestContext.Current.CancellationToken);
            }
        }
    }

    /// <summary>An attachment's picture, by the attachment's id.</summary>
    private static Drawn APicture(string id) => Drawn.Attached(APost.APicture(id: id));

    /// <summary>A picture a frame wants that is near the screen but not on it.</summary>
    private static WantedPicture Near(Drawn drawn) => new(drawn, OnScreen: false);

    /// <summary>A picture a frame wants that is on screen in it.</summary>
    private static WantedPicture OnScreen(Drawn drawn) => new(drawn, OnScreen: true);

    /// <summary>A terminal that draws pictures, since none of these tests is about one that does not.</summary>
    private static CellSize? ADrawingTerminal() => new CellSize(10, 20);

    private static byte[] APng(int width, int height) => Encoded(width, height, new PngEncoder());

    private static byte[] AJpeg(int width, int height) => Encoded(width, height, new JpegEncoder());

    /// <summary>
    ///     A PNG of noise, which no compressor can shrink: a file about as large as its pixels, where a blank picture's
    ///     file is a few hundred bytes whatever its size.
    /// </summary>
    private static byte[] ANoisyPng(int width, int height)
    {
        var noise = new Random(345);

        using var image = new Image<Rgba32>(width, height);
        using var bytes = new MemoryStream();

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                foreach (ref var pixel in rows.GetRowSpan(y))
                {
                    pixel = new Rgba32((byte)noise.Next(256), (byte)noise.Next(256), (byte)noise.Next(256));
                }
            }
        });

        image.Save(bytes, new PngEncoder());

        return bytes.ToArray();
    }

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
}
