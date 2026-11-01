using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Wooly.Tui.Media;
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tests.Fakes;

/// <summary>
///     A picture's pixels, said outright: for a test about what becomes of them on the way to the terminal, whose
///     expected pixels are worked out from these by hand.
/// </summary>
internal static class APicture
{
    /// <summary>
    ///     A picture <paramref name="width" /> by <paramref name="height" />, every pixel <paramref name="colour" />.
    /// </summary>
    public static Picture Solid(int width, int height, Color colour)
    {
        var pixels = new Color[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                pixels[x, y] = colour;
            }
        }

        return new Picture(pixels);
    }

    /// <summary>A colour as its four channels, which an assertion prints where two differ, as a colour is not.</summary>
    public static (byte R, byte G, byte B, byte A) Rgba(Color colour) => (colour.R, colour.G, colour.B, colour.A);

    /// <summary>The pixels <paramref name="png" /> holds, transparency and all.</summary>
    public static Picture FromPng(byte[] png)
    {
        using var image = Image.Load<Rgba32>(png);
        var pixels = new Color[image.Width, image.Height];

        for (var x = 0; x < image.Width; x++)
        {
            for (var y = 0; y < image.Height; y++)
            {
                var pixel = image[x, y];

                pixels[x, y] = new Color(pixel.R, pixel.G, pixel.B, pixel.A);
            }
        }

        return new Picture(pixels);
    }
}
