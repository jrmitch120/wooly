using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Tui;

/// <summary>
///     The one step that takes a picture to its box's pixels, on the sixel path and the Kitty path alike (#438): the
///     picture's middle cropped to the box's shape, then resized — so a picture of another shape fills its box rather
///     than being stretched to it, and one already the box's shape comes out as a plain resize. A Stand-in's blur is
///     stretched to its box instead, and a custom emoji fitted whole inside it.
/// </summary>
public class FilledPictureTests
{
    /// <summary>A Media row's box: three columns by one row, at a cell twice as tall as it is wide.</summary>
    private static readonly CellSize Cell = new(10, 20);

    private static readonly Color Red = new(255, 0, 0);
    private static readonly Color Green = new(0, 255, 0);
    private static readonly Color Blue = new(0, 0, 255);
    private static readonly Color White = new(255, 255, 255);
    private static readonly Color Black = new(0, 0, 0);

    [Fact]
    public void Scaled_KeepsAPortraitPicturesMiddleRatherThanSquashingIt()
    {
        var filled = PictureDecoder.Scaled(Portrait(), 3 * Cell.Width, 1 * Cell.Height, BoxFill.Cropped);

        AssertTheMiddleKept(filled);
    }

    [Fact]
    public void Placeholders_SendAPortraitPicturesMiddleRatherThanSquashingIt()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = new Placeholders(terminal, () => { }, work => work());
        var box = new Inset(new Drawn("m1", "https://files.example/m1.png"), Column: 0, Columns: 3, Rows: 1);

        placeholders.Ready(box, Portrait(), Cell);

        AssertTheMiddleKept(Pixels(Assert.Single(terminal.Transmitted).Png));
    }

    /// <summary>
    ///     A picture already its box's shape loses nothing to the crop: each quarter of it is still a quarter of what
    ///     comes out, to the corners.
    /// </summary>
    [Fact]
    public void Scaled_ResizesAPictureAlreadyTheBoxsShapeWhole()
    {
        var filled = PictureDecoder.Scaled(Quartered(60, 40), 30, 20, BoxFill.Cropped);

        Assert.Equal((30, 20), (filled.Width, filled.Height));
        Assert.Equal(Red, filled.Pixels[0, 0]);
        Assert.Equal(Green, filled.Pixels[29, 0]);
        Assert.Equal(Blue, filled.Pixels[0, 19]);
        Assert.Equal(White, filled.Pixels[29, 19]);
        Assert.Equal(Red, filled.Pixels[10, 6]);
        Assert.Equal(White, filled.Pixels[20, 14]);
    }

    /// <summary>
    ///     A <b>Stand-in</b>'s blur is decoded square and stretched to its box, not cropped like the picture it stands
    ///     in for: stretching it is what gives it back the picture's proportions, so it lines up with what replaces it
    ///     (ADR-0025). Set into a wide shaped box, it keeps its full height — its top band and its bottom band both.
    /// </summary>
    [Fact]
    public void Placeholders_StretchABlurToAWideBoxRatherThanCroppingIt()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = new Placeholders(terminal, () => { }, work => work());

        placeholders.Ready(WideBlur(), Banded(), Cell);

        AssertNothingCropped(Pixels(Assert.Single(terminal.Transmitted).Png));
    }

    /// <inheritdoc cref="Placeholders_StretchABlurToAWideBoxRatherThanCroppingIt" />
    [Fact]
    public void SixelPictures_StretchABlurToAWideBoxRatherThanCroppingIt()
    {
        var sixels = new SixelPictures(encode: (_, _) => "", elsewhere: work => work());

        var sixel = sixels.Of(WideBlur(), Banded(), Cell, new SixelCrop(Top: 0, Rows: 2, Columns: 8), colours: 256);

        AssertNothingCropped(new Picture(sixel.Pixels));
    }

    /// <summary>
    ///     On sixel a custom emoji is fitted whole into its box and centred, as through Kitty's placeholders, and the
    ///     rest of the box is the page it is laid on rather than black: twice as wide as it is tall, in its two cells by
    ///     one of 20 by 40 pixels, its middle twenty rows are its colour edge to edge and the ten above and below the
    ///     backdrop.
    /// </summary>
    [Fact]
    public void SixelPictures_FitAWideCustomEmojiWholeIntoItsBoxOnTheBackdrop()
    {
        var page = new Color(30, 30, 46);
        var sixels = new SixelPictures(encode: (_, _) => "", elsewhere: work => work(), backdrop: () => page);
        var emoji = new Inset(
            Drawn.CustomEmoji(new Wooly.Core.CustomEmoji("php", "https://files.example/emoji/php.png")),
            Column: 0,
            CustomEmojiBox.Columns,
            CustomEmojiBox.Rows);

        var sixel = sixels.Of(
            emoji,
            CustomEmojiTests.Solid(72, 36),
            new CellSize(20, 40),
            new SixelCrop(Top: 0, Rows: 1, Columns: 2),
            colours: 256);

        Assert.Equal((40, 40), (sixel.Pixels.GetLength(0), sixel.Pixels.GetLength(1)));

        for (var x = 0; x < 40; x++)
        {
            for (var y = 0; y < 40; y++)
            {
                Assert.Equal(y is >= 10 and < 30 ? CustomEmojiTests.Solid(1, 1).Pixels[0, 0] : page, sixel.Pixels[x, y]);
            }
        }
    }

    /// <summary>An avatar's fixed box is still filled by its picture's middle, as an instance crops one.</summary>
    [Fact]
    public void Placeholders_SendAPortraitAvatarsMiddleRatherThanFittingIt()
    {
        var terminal = new FakeTerminalImages();
        using var placeholders = new Placeholders(terminal, () => { }, work => work());
        var box = new Inset(
            Drawn.Avatar("jeff@mastodon.social", "https://files.example/jeff.png"),
            Column: 0,
            Columns: 3,
            Rows: 1);

        placeholders.Ready(box, Portrait(), Cell);

        AssertTheMiddleKept(Pixels(Assert.Single(terminal.Transmitted).Png));
    }

    /// <summary>
    ///     A blur set into a shaped box eight columns by two rows: 80 by 40 pixels, twice as wide as it is tall.
    /// </summary>
    private static Inset WideBlur()
    {
        var picture = new Drawn("m1", "https://files.example/m1.png");

        return new Inset(Drawn.Blur(picture), Column: 0, Columns: 8, Rows: 2) { Blur = Banded() };
    }

    /// <summary>
    ///     A blur as <see cref="Blurs" /> decodes one, 32 pixels square: red along its top eight rows, blue along its
    ///     bottom eight, and green between.
    /// </summary>
    private static Picture Banded()
    {
        var pixels = new Color[Blurs.Side, Blurs.Side];

        for (var x = 0; x < Blurs.Side; x++)
        {
            for (var y = 0; y < Blurs.Side; y++)
            {
                pixels[x, y] = y < 8 ? Red : y >= Blurs.Side - 8 ? Blue : Green;
            }
        }

        return new Picture(pixels);
    }

    /// <summary>
    ///     The banded blur at 80 by 40: its red band still along the top row and its blue band along the bottom one,
    ///     where cropping it to the box's shape would have cut both away.
    /// </summary>
    private static void AssertNothingCropped(Picture stretched)
    {
        Assert.Equal((80, 40), (stretched.Width, stretched.Height));

        foreach (var x in new[] { 0, 40, 79 })
        {
            Assert.Equal(Red, stretched.Pixels[x, 0]);
            Assert.Equal(Green, stretched.Pixels[x, 20]);
            Assert.Equal(Blue, stretched.Pixels[x, 39]);
        }
    }

    /// <summary>
    ///     A portrait picture, 100 by 400: a red band along the top, a blue one along the bottom, and between them a
    ///     green middle with a white square 40 pixels a side at its centre.
    /// </summary>
    private static Picture Portrait()
    {
        var pixels = new Color[100, 400];

        for (var x = 0; x < 100; x++)
        {
            for (var y = 0; y < 400; y++)
            {
                pixels[x, y] = y < 150 ? Red
                    : y >= 250 ? Blue
                    : x is >= 30 and < 70 && y is >= 180 and < 220 ? White
                    : Green;
            }
        }

        return new Picture(pixels);
    }

    /// <summary>
    ///     Red at the top left, green at the top right, blue at the bottom left, white at the bottom right.
    /// </summary>
    private static Picture Quartered(int width, int height)
    {
        var pixels = new Color[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var (right, lower) = (x >= width / 2, y >= height / 2);

                pixels[x, y] = (right, lower) switch
                {
                    (false, false) => Red,
                    (true, false) => Green,
                    (false, true) => Blue,
                    _ => White,
                };
            }
        }

        return new Picture(pixels);
    }

    /// <summary>
    ///     Thirty by twenty pixels of the portrait's middle: none of its red or blue bands, and its white square still
    ///     square — as wide as it is tall, give or take the resize's softened edges.
    /// </summary>
    private static void AssertTheMiddleKept(Picture filled)
    {
        Assert.Equal((30, 20), (filled.Width, filled.Height));

        var colours = Enumerable.Range(0, 30).SelectMany(x => Enumerable.Range(0, 20).Select(y => filled.Pixels[x, y]));

        Assert.DoesNotContain(colours, colour => colour.R > colour.G + 64 && colour.R > colour.B + 64);
        Assert.DoesNotContain(colours, colour => colour.B > colour.G + 64 && colour.B > colour.R + 64);

        bool Whitish(Color colour) => colour is { R: > 200, G: > 200, B: > 200 };

        var across = Enumerable.Range(0, 30).Count(x => Whitish(filled.Pixels[x, 10]));
        var down = Enumerable.Range(0, 20).Count(y => Whitish(filled.Pixels[15, y]));

        Assert.InRange(across, 10, 14);
        Assert.InRange(down, 10, 14);
        Assert.InRange(across - down, -2, 2);
    }

    private static Picture Pixels(byte[] png)
    {
        using var image = Image.Load<Rgba32>(png);
        var pixels = new Color[image.Width, image.Height];

        for (var x = 0; x < image.Width; x++)
        {
            for (var y = 0; y < image.Height; y++)
            {
                var pixel = image[x, y];

                pixels[x, y] = new Color(pixel.R, pixel.G, pixel.B);
            }
        }

        return new Picture(pixels);
    }
}
