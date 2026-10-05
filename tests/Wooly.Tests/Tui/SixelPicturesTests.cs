using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Tui;

/// <summary>
///     A picture's sixel, encoded once for each part of its box that is on the page and kept (#292). Sixel cannot move
///     an image already on screen, so every scroll step sends each visible picture again; what this saves is encoding
///     it again too — the whole of it on every move, and a crop of it on every move of a box half off the page.
/// </summary>
public class SixelPicturesTests
{
    private static readonly CellSize Cell = new(10, 20);

    [Fact]
    public void Of_EncodesAPartOfABoxOnceHoweverOftenItIsDrawn()
    {
        var encodes = 0;
        var sixels = new SixelPictures((_, _) => $"sixel {++encodes}");
        var inset = Box("m1", columns: 8, rows: 4);
        var picture = APicture(160, 160);

        var first = sixels.Of(inset, picture, Cell, new SixelCrop(Top: 0, Rows: 4, Columns: 8), colours: 64);
        var again = sixels.Of(inset, picture, Cell, new SixelCrop(Top: 0, Rows: 4, Columns: 8), colours: 64);

        Assert.Same(first, again);
        Assert.Equal(1, encodes);
        Assert.Equal("sixel 1", first.Encoded);
    }

    /// <summary>The pixels are the box's in pixels, scaled once — not the picture's own, scaled by the terminal.</summary>
    [Fact]
    public void Of_IsTheBoxInPixels()
    {
        var sixels = new SixelPictures((_, _) => "");

        var sixel = sixels.Of(Box("m1", 8, 4), APicture(300, 200), Cell, new SixelCrop(0, 4, 8), colours: 64);

        Assert.Equal((80, 80), (sixel.Pixels.GetLength(0), sixel.Pixels.GetLength(1)));
    }

    /// <summary>
    ///     A box half off the page is only the rows still on it, cut from the box's pixels at a cell's height a row —
    ///     each crop encoded once, so scrolling back over one is free.
    /// </summary>
    [Fact]
    public void Of_CutsTheRowsOnThePageAndKeepsEachCrop()
    {
        var encoded = new List<(int Width, int Height)>();
        var sixels = new SixelPictures((pixels, _) =>
        {
            encoded.Add((pixels.GetLength(0), pixels.GetLength(1)));

            return "";
        });
        var inset = Box("m1", columns: 8, rows: 4);
        var picture = APicture(80, 80);

        var lower = sixels.Of(inset, picture, Cell, new SixelCrop(Top: 1, Rows: 3, Columns: 8), colours: 64);
        sixels.Of(inset, picture, Cell, new SixelCrop(Top: 2, Rows: 2, Columns: 5), colours: 64);
        sixels.Of(inset, picture, Cell, new SixelCrop(Top: 1, Rows: 3, Columns: 8), colours: 64);

        Assert.Equal([(80, 60), (50, 40)], encoded);
        Assert.Equal((80, 60), (lower.Pixels.GetLength(0), lower.Pixels.GetLength(1)));
    }

    /// <summary>The crop is the right part of the picture, not merely the right size of it.</summary>
    [Fact]
    public void Of_CutsFromWhereTheCropBegins()
    {
        var pixels = new Color[80, 80];

        pixels[3, 20] = new Color(255, 0, 0);

        var sixel = new SixelPictures((_, _) => "")
            .Of(Box("m1", 8, 4), new Picture(pixels), Cell, new SixelCrop(Top: 1, Rows: 3, Columns: 8), colours: 64);

        Assert.Equal(new Color(255, 0, 0), sixel.Pixels[3, 0]);
    }

    /// <summary>
    ///     A crop whose preparing failed is encoded on the frame instead — never waited for, which with nothing coming
    ///     would be a frame waiting for ever.
    /// </summary>
    [Fact]
    public void Of_EncodesACropItselfWhereItsPreparingFailed()
    {
        var failing = true;
        var sixels = new SixelPictures(
            (_, _) => failing ? throw new InvalidOperationException("No encoder today.") : "sixel",
            work => work());
        var inset = Box("m1", columns: 8, rows: 4);
        var crop = new SixelCrop(Top: 0, Rows: 4, Columns: 8);

        sixels.Prepare(inset, APicture(80, 80), Cell, crop, colours: 64);
        failing = false;

        Assert.Equal("sixel", sixels.Of(inset, APicture(80, 80), Cell, crop, colours: 64).Encoded);
    }

    /// <summary>
    ///     A rough cut, for a page that is moving, is the same crop at half the resolution and in at most
    ///     <see cref="SixelPictures.RoughColours" /> — kept apart from the sharp one, so each is encoded once (#342).
    /// </summary>
    [Fact]
    public void Of_RoughIsTheCropAtHalfTheResolutionInFewerColours()
    {
        var encoded = new List<(Color[,] Pixels, int Colours)>();
        var sixels = new SixelPictures((pixels, colours) =>
        {
            encoded.Add((pixels, colours));

            return $"{colours}";
        });
        var pixels = new Color[80, 80];

        for (var x = 0; x < 80; x++)
        {
            for (var y = 0; y < 80; y++)
            {
                pixels[x, y] = new Color(x * 3, y * 3, 0);
            }
        }

        var inset = Box("m1", columns: 8, rows: 4);
        var crop = new SixelCrop(Top: 0, Rows: 4, Columns: 8);

        var sharp = sixels.Of(inset, new Picture(pixels), Cell, crop, colours: 256);
        var rough = sixels.Of(inset, new Picture(pixels), Cell, crop, colours: 256, rough: true);
        sixels.Of(inset, new Picture(pixels), Cell, crop, colours: 256, rough: true);

        Assert.Equal(["256", "64"], [sharp.Encoded, rough.Encoded]);
        Assert.Equal(2, encoded.Count);

        var coarse = encoded[1].Pixels;

        Assert.Equal((80, 80), (coarse.GetLength(0), coarse.GetLength(1)));
        Assert.Equal(coarse[0, 0], coarse[1, 1]);
        Assert.Equal(new Color(1, 1, 0), coarse[0, 0]);
        Assert.NotEqual(coarse[1, 1], coarse[2, 2]);

        // What the driver is handed as the cut's pixels is the crop itself: only the encoding is rough.
        Assert.Equal(pixels[1, 1], rough.Pixels[1, 1]);
    }

    /// <summary>
    ///     A crop prepared with somebody waiting on it says so once it is held, and only to the call that started it —
    ///     the sharp cut of a still page is asked for on every frame until it is ready, and is announced once.
    /// </summary>
    [Fact]
    public void Prepare_SaysOnceWhenTheCropIsHeld()
    {
        var elsewhere = new List<Action>();
        var sixels = new SixelPictures((_, _) => "sixel", elsewhere.Add);
        var inset = Box("m1", columns: 8, rows: 4);
        var crop = new SixelCrop(Top: 0, Rows: 4, Columns: 8);
        var told = 0;

        sixels.Prepare(inset, APicture(80, 80), Cell, crop, colours: 256, ready: () => told++);
        sixels.Prepare(inset, APicture(80, 80), Cell, crop, colours: 256, ready: () => told++);

        Assert.Null(sixels.Held(inset, Cell, crop, colours: 256));
        Assert.Equal(0, told);

        Assert.Single(elsewhere)();

        Assert.NotNull(sixels.Held(inset, Cell, crop, colours: 256));
        Assert.Equal(1, told);
    }

    /// <summary>A crop is a megabyte or so, so only so many are kept; the one used longest ago goes first.</summary>
    [Fact]
    public void Of_KeepsNoMoreThanItHasRoomFor()
    {
        var encodes = 0;
        var sixels = new SixelPictures((_, _) => $"{++encodes}");
        var picture = APicture(20, 20);

        for (var at = 0; at <= SixelPictures.MostHeld; at++)
        {
            sixels.Of(Box($"m{at}", 2, 1), picture, Cell, new SixelCrop(0, 1, 2), colours: 64);
        }

        // The most recent is still held; the first was let go of and is encoded again.
        sixels.Of(Box($"m{SixelPictures.MostHeld}", 2, 1), picture, Cell, new SixelCrop(0, 1, 2), colours: 64);
        Assert.Equal(SixelPictures.MostHeld + 1, encodes);

        sixels.Of(Box("m0", 2, 1), picture, Cell, new SixelCrop(0, 1, 2), colours: 64);
        Assert.Equal(SixelPictures.MostHeld + 2, encodes);
    }

    /// <summary>
    ///     A crop prepared ahead — the one a scroll of a row will want next — is encoded elsewhere, once, and is there
    ///     when it is asked for rather than encoded on the frame that wants it.
    /// </summary>
    [Fact]
    public void Prepare_EncodesACropElsewhereSoItIsReadyWhenAskedFor()
    {
        var encodes = 0;
        var elsewhere = new List<Action>();
        var sixels = new SixelPictures((_, _) => $"{++encodes}", elsewhere.Add);
        var inset = Box("m1", columns: 8, rows: 4);
        var picture = APicture(80, 80);

        sixels.Prepare(inset, picture, Cell, new SixelCrop(Top: 1, Rows: 3, Columns: 8), colours: 64);
        sixels.Prepare(inset, picture, Cell, new SixelCrop(Top: 1, Rows: 3, Columns: 8), colours: 64);

        Assert.Equal(0, encodes);

        Assert.Single(elsewhere)();

        var sixel = sixels.Of(inset, picture, Cell, new SixelCrop(Top: 1, Rows: 3, Columns: 8), colours: 64);

        Assert.Equal(1, encodes);
        Assert.Equal("1", sixel.Encoded);
    }

    /// <summary>
    ///     Asked for while it is still being prepared, a crop is waited for rather than encoded a second time on the
    ///     frame — which would be the very work preparing it was to save.
    /// </summary>
    [Fact]
    public async Task Of_WaitsForACropBeingPreparedRatherThanEncodingItAgain()
    {
        var encodes = 0;
        var started = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var sixels = new SixelPictures(
            (_, _) =>
            {
                started.Set();
                release.Wait();

                return $"{Interlocked.Increment(ref encodes)}";
            },
            work => Task.Run(work));
        var inset = Box("m1", columns: 8, rows: 4);
        var picture = APicture(80, 80);
        var crop = new SixelCrop(Top: 0, Rows: 4, Columns: 8);

        sixels.Prepare(inset, picture, Cell, crop, colours: 64);
        started.Wait(TestContext.Current.CancellationToken);

        var asked = Task.Run(() => sixels.Of(inset, picture, Cell, crop, colours: 64), TestContext.Current.CancellationToken);

        release.Set();

        Assert.Equal("1", (await asked).Encoded);
        Assert.Equal(1, encodes);
    }

    private static Inset Box(string id, int columns, int rows) =>
        new(new Drawn(id, $"https://files.example/{id}.png"), Column: 0, columns, rows);

    private static Picture APicture(int width, int height) => new(new Color[width, height]);
}
