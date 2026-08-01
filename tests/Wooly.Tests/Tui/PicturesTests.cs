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

        Assert.Null(pictures.Of(media));

        await landed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.NotNull(pictures.Of(media));
        Assert.NotNull(pictures.Of(media));

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

        pictures.Of(APost.APicture() with { Preview = null });

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

        Assert.Null(pictures.Of(APost.APicture()));
        Assert.Null(pictures.Of(APost.APicture()));
        Assert.Null(pictures.Of(APost.APicture()));

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

        pictures.Of(APost.APicture(id: "m1"));
        pictures.Of(APost.APicture(id: "m2"));

        await both.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.NotNull(pictures.Of(APost.APicture(id: "m1")));
        Assert.NotNull(pictures.Of(APost.APicture(id: "m2")));
    }

    /// <summary>
    ///     A morning's scrolling is not held in memory. Past what it has room for, the attachment asked for longest
    ///     ago is the one dropped, and the ones since are all still there.
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
            () => { });

        // One more than there is room for, which drops the first.
        for (var at = 0; at <= Pictures.MostHeld; at++)
        {
            pictures.Of(APost.APicture(id: $"m{at}"));
        }

        Assert.Equal(Pictures.MostHeld + 1, asks);

        // The most recent is still remembered, so asking again sends for nothing.
        pictures.Of(APost.APicture(id: $"m{Pictures.MostHeld}"));

        Assert.Equal(Pictures.MostHeld + 1, asks);

        // The first is gone, so asking again sends for it again — which is what having dropped it means.
        pictures.Of(APost.APicture(id: "m0"));

        Assert.Equal(Pictures.MostHeld + 2, asks);
    }

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
}
