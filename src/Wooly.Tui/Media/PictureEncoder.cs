using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Wooly.Tui.Media;

/// <summary>
///     A decoded picture written back out as a PNG at the size of the box it is drawn in, which is what a Kitty terminal
///     is sent (ADR-0022). <see cref="PictureDecoder" />'s other half.
/// </summary>
internal static class PictureEncoder
{
    /// <summary>
    ///     <paramref name="picture" /> filling exactly <paramref name="width" /> by <paramref name="height" /> pixels,
    ///     cropped to that shape — or, for a Stand-in's blur, stretched to it (<see cref="PictureDecoder.Filled" />) —
    ///     as a PNG.
    /// </summary>
    /// <remarks>
    ///     Exactly, because a picture sent at any other size is one the terminal scales again. Fast compression rather
    ///     than small, since the PNG goes down a pipe to the terminal on the same machine rather than over a network.
    /// </remarks>
    /// <param name="picture">The picture, or the blur.</param>
    /// <param name="width">How wide the box is, in pixels.</param>
    /// <param name="height">How tall the box is, in pixels.</param>
    /// <param name="stretched">
    ///     Whether this is a Stand-in's blur, which is stretched to the box rather than cropped.
    /// </param>
    public static byte[] Png(Picture picture, int width, int height, bool stretched)
    {
        using var image = PictureDecoder.Filled(picture, width, height, stretched);
        using var png = new MemoryStream();

        image.SaveAsPng(png, new PngEncoder { CompressionLevel = PngCompressionLevel.BestSpeed });

        return png.ToArray();
    }
}
