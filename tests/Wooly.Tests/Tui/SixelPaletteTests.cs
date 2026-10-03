using Wooly.Tui.Media;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Tui;

/// <summary>
///     The colours a sixel is drawn in, chosen from the whole picture (#292). Terminal.Gui's own choice stopped at the
///     first 64 colours it met reading down from the left edge, so a dark sign in the middle of a pale illustration came
///     out salmon in WezTerm: there was nothing dark in the palette to draw it with.
/// </summary>
public class SixelPaletteTests
{
    /// <summary>The bug as it was seen: everything on the left is pale and varied, and the dark is all on the right.</summary>
    [Fact]
    public void Quantized_DrawsWhatIsOnlyOnTheRightOfThePictureInAColourNearItsOwn()
    {
        var darkGreen = new Color(30, 80, 70);
        var pixels = new Color[200, 100];

        for (var x = 0; x < 200; x++)
        {
            for (var y = 0; y < 100; y++)
            {
                pixels[x, y] = x < 120 ? new Color(150 + (x % 100), 200 + (y % 50), 180 + ((x + y) % 70)) : darkGreen;
            }
        }

        var (quantized, palette) = SixelPalette.Quantized(pixels, colours: 64);

        Assert.InRange(palette.Colours.Count, 2, 64);
        Assert.Contains(palette.Colours, colour => Distance(colour, darkGreen) < 20);
        Assert.True(Distance(quantized[160, 50], darkGreen) < 20);
    }

    /// <summary>
    ///     Every pixel handed to the encoder is a palette colour exactly, which it finds at once, rather than one whose
    ///     nearest it searches the palette for.
    /// </summary>
    [Fact]
    public void Quantized_LeavesEveryPixelAColourOfThePalette()
    {
        var pixels = new Color[40, 30];

        for (var x = 0; x < 40; x++)
        {
            for (var y = 0; y < 30; y++)
            {
                pixels[x, y] = new Color(x * 6, y * 8, (x * y) % 256);
            }
        }

        var (quantized, palette) = SixelPalette.Quantized(pixels, colours: 16);

        Assert.InRange(palette.Colours.Count, 1, 16);

        foreach (var pixel in quantized)
        {
            Assert.Contains(pixel, palette.Colours);
        }
    }

    /// <summary>A picture of fewer colours than there is room for is drawn in exactly its own.</summary>
    [Fact]
    public void Quantized_KeepsEveryColourOfAPictureWithFewerThanThereIsRoomFor()
    {
        Color[,] pixels = { { new(255, 0, 0), new(0, 255, 0) }, { new(0, 0, 255), new(255, 0, 0) } };

        var (quantized, _) = SixelPalette.Quantized(pixels, colours: 64);

        Assert.Equal(pixels, quantized);
    }

    private static double Distance(Color one, Color other) =>
        Math.Sqrt(Math.Pow(one.R - other.R, 2) + Math.Pow(one.G - other.G, 2) + Math.Pow(one.B - other.B, 2));
}
