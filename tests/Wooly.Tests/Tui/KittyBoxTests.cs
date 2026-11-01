using Terminal.Gui.Drivers;
using Wooly.Core.Posts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Media;
using Wooly.Tui.Theme;
using static Wooly.Tests.Tui.ContentClicks;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Tui;

/// <summary>
///     Pictures on Kitty through a box — a terminal that speaks the Kitty graphics protocol and is not known by name to
///     draw its placeholders — read back off what the whole shell, drawn headless, hands the terminal (#533): each taken
///     to its box's size in pixels by the one step the other two ways take it by (<c>PictureDecoder.Filled</c>), so that
///     the terminal has nothing left to stretch.
/// </summary>
public class KittyBoxTests
{
    /// <summary>A cell that is not 1:2, so that a box square in cells is far from square in pixels.</summary>
    private static readonly CellSize Cell = new(20, 45);

    /// <summary>
    ///     A post's picture is handed over at exactly its box's size in pixels, cropped to it from the middle as on the
    ///     other ways: a picture 400 by 110 fits its box only to within a row — 54 columns by 6.6 rows, rounded to 7 —
    ///     and the terminal stretched it down the rest.
    /// </summary>
    [Fact]
    public async Task APostsPictureIsHandedOverCroppedToItsBoxsSizeInPixels()
    {
        var picture = Quartered(400, 110);

        using var drawn = await Kitty(
            new FakePictures().Holding("m1", picture),
            APost.With(id: "110", media: [APost.APicture("m1", shape: new PictureShape(400, 110))]));

        var image = Assert.Single(Images(drawn));
        var (width, height) = InPixels(image);

        Assert.Equal(Size(image.Pixels!), (width, height));
        Assert.Equal(Colours(PictureDecoder.Scaled(picture, width, height, BoxFill.Cropped)), Colours(image.Pixels!));
    }

    /// <summary>
    ///     A Stand-in's blur, decoded square, is handed over stretched to its box's size in pixels, as on the other
    ///     ways, which gives it back the picture's proportions — not cropped to its middle band.
    /// </summary>
    [Fact]
    public async Task AStandInsBlurIsHandedOverStretchedToItsBoxsSizeInPixels()
    {
        using var drawn = await Kitty(
            new FakePictures(),
            APost.With(
                id: "110",
                media: [APost.APicture("m1", shape: new PictureShape(800, 200), blurhash: StandInBlurTests.AHash)]));

        var image = Assert.Single(Images(drawn));
        var (width, height) = InPixels(image);
        var blur = new Blurs().Of(StandInBlurTests.AHash)!;

        Assert.Equal(Size(image.Pixels!), (width, height));
        Assert.Equal(Colours(PictureDecoder.Scaled(blur, width, height, BoxFill.Stretched)), Colours(image.Pixels!));
        Assert.NotEqual(Colours(PictureDecoder.Scaled(blur, width, height, BoxFill.Cropped)), Colours(image.Pixels!));
    }

    /// <summary>
    ///     A window resized wider widens a post's picture's box, and the picture is handed over again at the new box's
    ///     size in pixels — not left at the first one's for the terminal to stretch.
    /// </summary>
    [Fact]
    public async Task AResizedWindowHandsAPictureOverAgainAtItsNewBoxsSize()
    {
        using var drawn = await Kitty(
            new FakePictures().Holding("m1", Quartered(800, 200)),
            APost.With(id: "110", media: [APost.APicture("m1", shape: new PictureShape(800, 200))]));

        var before = Assert.Single(Images(drawn)).DestinationCells;

        drawn.Resize(120, Tall);

        var image = Assert.Single(Images(drawn));

        Assert.NotEqual(before.Size, image.DestinationCells.Size);
        Assert.Equal(InPixels(image), Size(image.Pixels!));
    }

    /// <summary>
    ///     The feed on Kitty through a box at <see cref="Cell" />, with <paramref name="posts" />, the first of them
    ///     picked.
    /// </summary>
    private static Task<DrawnShell> Kitty(FakePictures pictures, params Post[] posts) =>
        DrawnShell.Of(
            80,
            Tall,
            Themes.Dark,
            new AShell { Timelines = FakeTimelineReader.Holding(posts) },
            pictures: pictures,
            answersKitty: true,
            raster: ARaster.Kitty(Cell));

    /// <summary>Every picture the terminal was handed through a box.</summary>
    private static IEnumerable<RasterImageCommand> Images(DrawnShell drawn) =>
        drawn.Application.Driver!.GetOutputBuffer().GetRasterImages();

    /// <summary>How big the cells <paramref name="image" /> is placed over are, in pixels.</summary>
    private static (int Width, int Height) InPixels(RasterImageCommand image) =>
        (image.DestinationCells.Width * Cell.Width, image.DestinationCells.Height * Cell.Height);

    private static (int Width, int Height) Size(Color[,] pixels) => (pixels.GetLength(0), pixels.GetLength(1));

    private static List<(byte, byte, byte, byte)> Colours(Picture picture) => Colours(picture.Pixels);

    private static List<(byte, byte, byte, byte)> Colours(Color[,] pixels) =>
        [.. pixels.Cast<Color>().Select(APicture.Rgba)];

    /// <summary>Red at the top left, green at the top right, blue at the bottom left, white at the bottom right.</summary>
    private static Picture Quartered(int width, int height)
    {
        var pixels = new Color[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                pixels[x, y] = (x >= width / 2, y >= height / 2) switch
                {
                    (false, false) => new Color(255, 0, 0),
                    (true, false) => new Color(0, 255, 0),
                    (false, true) => new Color(0, 0, 255),
                    _ => new Color(255, 255, 255),
                };
            }
        }

        return new Picture(pixels);
    }
}
