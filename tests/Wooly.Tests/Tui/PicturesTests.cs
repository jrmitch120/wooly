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
            landed.SetResult);

        var media = APost.APicture();

        pictures.Want([Near(Drawn.Attached(media))], ADrawingTerminal, Wide);

        await landed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.NotNull(pictures.Of(Drawn.Attached(media)));
        Assert.NotNull(pictures.Of(Drawn.Attached(media)));

        pictures.Want([Near(Drawn.Attached(media))], ADrawingTerminal, Wide);

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
            () => { });

        pictures.Want([Near(Drawn.Attached(APost.APicture() with { Preview = null }))], ADrawingTerminal, Wide);

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
            () => { });

        pictures.Want([Near(Drawn.Attached(APost.APicture()))], ADrawingTerminal, Wide);
        pictures.Want([Near(Drawn.Attached(APost.APicture()))], ADrawingTerminal, Wide);
        pictures.Want([Near(Drawn.Attached(APost.APicture()))], ADrawingTerminal, Wide);

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
            () =>
            {
                if (Interlocked.Increment(ref landed) == 2)
                {
                    both.SetResult();
                }
            });

        pictures.Want(
            [Near(Drawn.Attached(APost.APicture(id: "m1"))), Near(Drawn.Attached(APost.APicture(id: "m2")))],
            ADrawingTerminal,
            Wide);

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
        var photograph = ANoisyPng(1000, 640);

        using var pictures = APictures(_ => photograph, out var landings, asked: asked);

        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));

        for (var at = 0; at <= fitting; at++)
        {
            pictures.Want([Near(APicture($"m{at}"))], ADrawingTerminal, Wide);
            await landings.Landed(at + 1);
        }

        // Its pixels went to make room in the decoded tier, and its file long before, since files of this size fill
        // the encoded tier's budget in fewer pictures than pixels fill the decoded tier's.
        Assert.Equal(["m0"], pictures.Drain());
        Assert.True(photograph.Length * (fitting + 1L) > Pictures.EncodedBudget);

        pictures.Want([Near(APicture("m0"))], ADrawingTerminal, Wide);
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

        using var pictures = APictures(_ => APng(1000, 640), out var landings);

        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));

        for (var at = 0; at < fitting; at++)
        {
            pictures.Want([Near(APicture($"m{at}"))], ADrawingTerminal, Wide);
            await landings.Landed(at + 1);
        }

        // The first fetched, wanted again: it is now the most recently wanted, and the second is the oldest.
        pictures.Want([Near(APicture("m0"))], ADrawingTerminal, Wide);
        pictures.Want([Near(APicture("new"))], ADrawingTerminal, Wide);
        await landings.Landed(fitting + 1);

        Assert.Equal(["m1"], pictures.Drain());
    }

    /// <summary>
    ///     What is on screen in the latest frame is never let go of, however far over its budget that leaves the
    ///     decoded tier. Once it is off screen it is ordinary again, and the farthest of what a frame wanted goes first.
    /// </summary>
    [Fact]
    public async Task Pictures_NeverLetsGoOfWhatIsOnScreen()
    {

        using var pictures = APictures(_ => APng(1000, 640), out var landings);

        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));
        var screenful = Enumerable.Range(0, fitting + 3).Select(at => APicture($"s{at}")).ToList();

        pictures.Want([.. screenful.Select(OnScreen)], ADrawingTerminal, Wide);
        await landings.Landed(fitting + 3);

        Assert.Empty(pictures.Drain());
        Assert.All(screenful, drawn => Assert.NotNull(pictures.Of(drawn)));

        // The page moves on: the next frame has none of them on screen, so the tier comes back to its budget, letting
        // go of the four of them farthest from where the screen was — three for the screenful, one for what is next.
        pictures.Want([Near(APicture("next"))], ADrawingTerminal, Wide);
        await landings.Landed(fitting + 4);

        Assert.Equal([$"s{fitting + 2}", $"s{fitting + 1}", $"s{fitting}", $"s{fitting - 1}"], pictures.Drain());
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

        using var pictures = APictures(_ => APng(1000, 640), out var landings, asked: asked);

        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));
        var frame = Enumerable.Range(0, fitting + 3).Select(at => Near(APicture($"m{at}"))).ToList();

        // Nothing's size is known before it is fetched, so the first frame finds out by decoding the lot.
        pictures.Want(frame, ADrawingTerminal, Wide);
        await landings.Landed(fitting + 3);

        Assert.Equal(3, pictures.Drain().Count);

        pictures.Want(frame, ADrawingTerminal, Wide);
        pictures.Want(frame, ADrawingTerminal, Wide);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(fitting + 3, landings.Count);
        Assert.Equal(fitting + 3, asked.Count);
        Assert.Empty(pictures.Drain());
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

        using var pictures = APictures(_ => APng(4000, 1000), out var landings, asked: asked);

        pictures.Want([OnScreen(APicture("m"))], ADrawingTerminal, width);
        await landings.Landed(1);

        Assert.Equal(500, pictures.Of(APicture("m"))?.Width);

        width = 100;
        pictures.Want([OnScreen(APicture("m"))], ADrawingTerminal, width);
        await landings.Landed(2);

        Assert.Equal(1000, pictures.Of(APicture("m"))?.Width);
        Assert.Single(asked);

        // Narrower again: the sharper pixels are drawn scaled down rather than decoded a third time.
        width = 50;
        pictures.Want([OnScreen(APicture("m"))], ADrawingTerminal, width);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(2, landings.Count);
        Assert.Equal(1000, pictures.Of(APicture("m"))?.Width);
    }

    /// <summary>
    ///     Pixels decoded again for a wider window replace the ones held, and those are said to be let go of like any
    ///     others: a Kitty terminal holding a copy decoded at the old size is told to drop it, rather than holding it
    ///     for the rest of the run beside the sharper copy sent in its place (ADR-0022).
    /// </summary>
    [Fact]
    public async Task Pictures_SaysThePixelsADecodeAgainReplacesAreLetGoOf()
    {
        var width = 50;

        using var pictures = APictures(_ => APng(4000, 1000), out var landings);

        pictures.Want([OnScreen(APicture("m"))], ADrawingTerminal, width);
        await landings.Landed(1);

        Assert.Empty(pictures.Drain());

        width = 100;
        pictures.Want([OnScreen(APicture("m"))], ADrawingTerminal, width);
        await landings.Landed(2);

        Assert.Equal(["m"], pictures.Drain());
        Assert.Equal(1000, pictures.Of(APicture("m"))?.Width);
        Assert.Empty(pictures.Drain());
    }

    /// <summary>
    ///     A picture let go of twice before anything drains — decoded again for a wider window, then again for a wider
    ///     one still — is handed back once: telling a Kitty terminal to forget it once forgets every size it holds.
    /// </summary>
    [Fact]
    public async Task Pictures_HandsBackAPictureLetGoOfTwiceBeforeADrainOnce()
    {
        var width = 50;

        using var pictures = APictures(_ => APng(4000, 1000), out var landings);

        pictures.Want([OnScreen(APicture("m"))], ADrawingTerminal, width);
        await landings.Landed(1);

        width = 100;
        pictures.Want([OnScreen(APicture("m"))], ADrawingTerminal, width);
        await landings.Landed(2);

        width = 150;
        pictures.Want([OnScreen(APicture("m"))], ADrawingTerminal, width);
        await landings.Landed(3);

        Assert.Equal(1500, pictures.Of(APicture("m"))?.Width);
        Assert.Equal(["m"], pictures.Drain());
    }

    /// <summary>
    ///     A picture is held at no more than the largest box this window could draw it in: the full width of the
    ///     window by the post screen's row cap, in pixels (ADR-0025). A hundred columns of ten-pixel cells is a thousand
    ///     pixels across, so a photograph four thousand across is held at a thousand, in its own proportions.
    /// </summary>
    [Fact]
    public async Task Pictures_HoldsAPictureAtTheLargestBoxThisWindowCouldDraw()
    {
        using var pictures = APictures(_ => APng(4000, 1000), out var landings);

        pictures.Want([Near(Drawn.Attached(APost.APicture()))], ADrawingTerminal, Wide);

        await landings.Landed(1);

        var picture = pictures.Of(Drawn.Attached(APost.APicture()));

        Assert.NotNull(picture);
        Assert.Equal(1000, picture.Width);
        Assert.Equal(250, picture.Height);
    }

    /// <summary>
    ///     Before the window has drawn, the content region has no columns to give, so a picture is held at the
    ///     decoder's own bounds rather than at nothing, as a terminal that has not said how big its cells are would.
    /// </summary>
    [Fact]
    public async Task Pictures_HoldsAPictureAtTheDecodersOwnBoundsBeforeTheWindowHasDrawn()
    {
        using var pictures = APictures(_ => APng(4000, 1000), out var landings);

        pictures.Want([OnScreen(APicture("m"))], ADrawingTerminal, 0);
        await landings.Landed(1);

        Assert.Equal(PictureDecoder.LongestSide, pictures.Of(APicture("m"))?.Width);
    }

    /// <summary>
    ///     An avatar is never drawn larger than an account screen's header box, eight cells by four, so that is all it
    ///     is held at: an avatar costs kilobytes, not the hundreds an instance's 400-pixel square would decode to.
    /// </summary>
    [Fact]
    public async Task Pictures_HoldsAnAvatarAtItsOwnBox()
    {
        using var pictures = APictures(_ => APng(400, 400), out var landings);

        pictures.Want(
            [Near(Drawn.Avatar("alice@example.social", "https://files.example/alice.png"))],
            ADrawingTerminal,
            Wide);

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

        using var pictures = APictures(_ => APng(1000, 640), out var landings, asked: asked);

        // Each a thousand by 640 at four bytes a pixel, which is the largest a hundred-column window draws them.
        var fitting = (int)(Pictures.DecodedBudget / (1000L * 640 * 4));

        for (var at = 0; at < fitting; at++)
        {
            pictures.Want([Near(APicture($"m{at}"))], ADrawingTerminal, Wide);
            await landings.Landed(at + 1);
        }

        Assert.Empty(pictures.Drain());

        pictures.Want([Near(APicture("over"))], ADrawingTerminal, Wide);
        await landings.Landed(fitting + 1);

        Assert.Equal(["m0"], pictures.Drain());
        Assert.Null(pictures.Of(APicture("m0")));

        pictures.Want([Near(APicture("m0"))], ADrawingTerminal, Wide);
        await landings.Landed(fitting + 2);

        Assert.NotNull(pictures.Of(APicture("m0")));
        Assert.Equal(fitting + 1, asked.Count);
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

        using var pictures = new Pictures(fetch.Fetch, () => { });

        var busy = Busy();

        pictures.Want([.. busy, Near(Picture("far"))], ADrawingTerminal, Wide);
        pictures.Want([.. busy, Near(Picture("near")), Near(Picture("far"))], ADrawingTerminal, Wide);

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

        using var pictures = new Pictures(fetch.Fetch, () => { });

        var busy = Busy();

        pictures.Want([.. busy, Near(Picture("left behind"))], ADrawingTerminal, Wide);
        pictures.Want([.. busy, Near(Picture("ahead"))], ADrawingTerminal, Wide);

        fetch.Answer("b0");
        await fetch.Until(Pictures.AtATime + 1);

        fetch.Answer("b1");
        fetch.Answer("b2");
        fetch.Answer("b3");
        fetch.Answer("ahead");

        pictures.Want([Near(Picture("left behind"))], ADrawingTerminal, Wide);
        await fetch.Until(Pictures.AtATime + 2);

        Assert.Equal(["ahead", "left behind"], fetch.Asked.Skip(Pictures.AtATime));
    }

    /// <summary>
    ///     A fetch already under way when its picture stops being wanted is let finish, and what it brings is held:
    ///     the bytes are already on their way, and the reader may yet turn round. Held as a file, undecoded, since no
    ///     frame has wanted its pixels since — so turning round is a decode rather than a second fetch (ADR-0025).
    /// </summary>
    [Fact]
    public async Task Pictures_HoldsWhatAnInFlightFetchBringsAfterItIsNoLongerWanted()
    {
        var fetch = new Gated();
        var landings = new Landings();

        using var pictures = new Pictures(fetch.Fetch, landings.Land);

        pictures.Want([Near(Picture("leaving"))], ADrawingTerminal, Wide);
        pictures.Want([Near(Picture("elsewhere"))], ADrawingTerminal, Wide);

        fetch.Answer("leaving", APng(4, 4));
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(0, landings.Count);
        Assert.Null(pictures.Of(Picture("leaving")));

        pictures.Want([Near(Picture("leaving"))], ADrawingTerminal, Wide);
        await landings.Landed(1);

        Assert.NotNull(pictures.Of(Picture("leaving")));
        Assert.Equal(["leaving", "elsewhere"], fetch.Asked);
    }

    /// <summary>
    ///     A picture waiting its turn to be decoded again from its file keeps that file however far over its budget the
    ///     encoded tier goes while it waits: letting go of it would turn the decode it is queued for into a second
    ///     fetch, which is what holding the file was for (ADR-0025).
    /// </summary>
    [Fact]
    public async Task Pictures_KeepsTheFileOfAPictureWaitingToBeDecodedAgain()
    {
        var fetch = new Gated();
        var landings = new Landings();
        var width = 50;

        using var pictures = new Pictures(fetch.Fetch, landings.Land);

        // A real picture with padding after it, so the file is most of the encoded tier on its own.
        var file = (byte[])[.. APng(4000, 1000), .. new byte[Pictures.EncodedBudget - (64 * Pictures.RememberingCost)]];

        pictures.Want([OnScreen(Picture("m"))], ADrawingTerminal, width);
        fetch.Answer("m", file);
        await landings.Landed(1);

        // Grown, so it is to be decoded again — but behind fetches that hold every turn, and alongside enough new
        // pictures that remembering them takes the encoded tier over its budget while it waits.
        width = 100;

        var more = Enumerable.Range(0, 128).Select(at => Near(Picture($"x{at}")));

        pictures.Want([.. Busy(), OnScreen(Picture("m")), .. more], ADrawingTerminal, width);

        fetch.Answer("b0");
        await landings.Landed(2);

        Assert.Equal(1000, pictures.Of(Picture("m"))?.Width);
        Assert.Single(fetch.Asked, address => address == "m");
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
        using var pictures = Pictures.Over(http, landed.SetResult);

        pictures.Want([Near(Drawn.Attached(APost.APicture()))], ADrawingTerminal, Wide);

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
        using var pictures = Pictures.Over(http, () => { });

        pictures.Want([Near(Drawn.Attached(APost.APicture()))], ADrawingTerminal, Wide);

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
        using var pictures = Pictures.Over(http, () => landed = true);

        pictures.Want([Near(Drawn.Attached(APost.APicture()))], ADrawingTerminal, Wide);

        // Nothing announces a refusal, so the wait is for the request to have been made and answered.
        while (network.Requests.Count == 0)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Null(pictures.Of(Drawn.Attached(APost.APicture())));
        Assert.False(landed);
    }

    /// <summary>
    ///     A cache over a fetch that answers with <paramref name="serve" />'s bytes for each address, writing down
    ///     every address it is asked for in <paramref name="asked" />.
    /// </summary>
    private static Pictures APictures(
        Func<string, byte[]?> serve,
        out Landings landings,
        List<string>? asked = null)
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
            landed.Land);
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

    /// <summary>
    ///     An attachment's picture, by the id the instance gave it — which is also the address it is fetched from.
    /// </summary>
    private static Drawn Picture(string id) => new(id, id);

    /// <summary>As many pictures as are fetched at once, named <c>b0</c> on, to keep every fetch busy.</summary>
    private static List<WantedPicture> Busy() =>
        [.. Enumerable.Range(0, Pictures.AtATime).Select(at => Near(Picture($"b{at}")))];

    /// <summary>A picture a frame wants that is near the screen but not on it.</summary>
    private static WantedPicture Near(Drawn drawn) => new(drawn, OnScreen: false);

    /// <summary>A picture a frame wants that is on screen in it.</summary>
    private static WantedPicture OnScreen(Drawn drawn) => new(drawn, OnScreen: true);

    /// <summary>
    ///     How wide a content region most of these tests say the frame has: a hundred columns of ten-pixel cells is a
    ///     thousand pixels across.
    /// </summary>
    private const int Wide = 100;

    /// <summary>A terminal that draws pictures, since most of these tests are not about one that does not.</summary>
    private static readonly Raster ADrawingTerminal = new(PictureWay.Kitty, new CellSize(10, 20), 0);

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

        /// <summary>
        ///     Lets the fetch of <paramref name="address" /> finish, with <paramref name="bytes" /> or nothing.
        /// </summary>
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
