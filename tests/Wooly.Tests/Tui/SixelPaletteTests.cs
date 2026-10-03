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
    public void BuildPalette_HasAColourForWhatIsOnlyOnTheRightOfThePicture()
    {
        var darkGreen = new Color(30, 80, 70);
        var colours = new List<Color>();

        // Read the way Terminal.Gui reads a picture into the builder: a column at a time, from the left.
        for (var x = 0; x < 200; x++)
        {
            for (var y = 0; y < 100; y++)
            {
                colours.Add(x < 120 ? new Color(150 + (x % 100), 200 + (y % 50), 180 + ((x + y) % 70)) : darkGreen);
            }
        }

        var palette = new SixelPalette().BuildPalette(colours, maxColors: 64);

        Assert.InRange(palette.Count, 2, 64);
        Assert.Contains(palette, colour => Distance(colour, darkGreen) < 20);
    }

    /// <summary>A picture of fewer colours than there is room for is drawn in exactly its own.</summary>
    [Fact]
    public void BuildPalette_KeepsEveryColourOfAPictureWithFewerThanThereIsRoomFor()
    {
        var colours = new List<Color> { new(255, 0, 0), new(0, 255, 0), new(0, 0, 255), new(255, 0, 0) };

        var palette = new SixelPalette().BuildPalette(colours, maxColors: 64);

        Assert.Equal(3, palette.Count);
        Assert.All(colours, colour => Assert.Contains(palette, chosen => Distance(chosen, colour) < 2));
    }

    [Fact]
    public void BuildPalette_IsEmptyForNoPicture() => Assert.Empty(new SixelPalette().BuildPalette([], maxColors: 64));

    private static double Distance(Color one, Color other) =>
        Math.Sqrt(Math.Pow(one.R - other.R, 2) + Math.Pow(one.G - other.G, 2) + Math.Pow(one.B - other.B, 2));
}
