using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Wooly.Tui.Media;

/// <summary>
///     A decoded picture written back out as a PNG at the size of the box it is drawn in, which is what a Kitty terminal
///     is sent (ADR-0022). <see cref="PictureDecoder" />'s other half.
/// </summary>
internal static class PictureEncoder
{
    /// <summary>
    ///     <paramref name="picture" /> resized to exactly <paramref name="width" /> by <paramref name="height" />
    ///     pixels, as a PNG.
    /// </summary>
    /// <remarks>
    ///     Exactly, because the box already has the picture's own shape (<see cref="Rendering.Inset.For" />): a picture
    ///     sent at any other size is one the terminal scales again, and a picture scaled to fit a box of another shape
    ///     is stretched. Fast compression rather than small, since the PNG goes down a pipe to the terminal on the same
    ///     machine rather than over a network.
    /// </remarks>
    public static byte[] Png(Picture picture, int width, int height)
    {
        using var image = new Image<Rgba32>(picture.Width, picture.Height);

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);

                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = picture.Pixels[x, y];

                    row[x] = new Rgba32(pixel.R, pixel.G, pixel.B, pixel.A);
                }
            }
        });

        if (image.Width != width || image.Height != height)
        {
            image.Mutate(context => context.Resize(width, height));
        }

        using var png = new MemoryStream();

        image.SaveAsPng(png, new PngEncoder { CompressionLevel = PngCompressionLevel.BestSpeed });

        return png.ToArray();
    }
}
