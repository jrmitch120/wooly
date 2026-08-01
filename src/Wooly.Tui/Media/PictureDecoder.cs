using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// Both libraries have a Color and this file is where they meet. The decoder's own is the one that needs no saying,
// because everything here is on its way to being drawn.
using Color = Terminal.Gui.Drawing.Color;

namespace Wooly.Tui.Media;

/// <summary>
///     Turns what an instance served — a PNG, a JPEG, a WebP — into the pixels a terminal can be given. Terminal.Gui
///     draws a <c>Color[,]</c> and decodes no file format at all, so this is the one place in the TUI that knows what a
///     JPEG is, and the only one that builds a colour without a theme answering for it (ADR-0016).
/// </summary>
internal static class PictureDecoder
{
    /// <summary>
    ///     How many pixels a decoded picture may be along its longer side. A picture is drawn into a box of a few dozen
    ///     cells — a few hundred pixels at the resolutions terminals report — so anything larger is decoded, held in
    ///     memory and thrown away again. Kept generous enough that the box is never upscaled from less than it has.
    /// </summary>
    public const int LongestSide = 320;

    /// <summary>
    ///     How many bytes of a download are worth reading. A preview is tens of kilobytes; anything of this size is
    ///     either not a preview or not worth the memory of finding out.
    /// </summary>
    public const int MostBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     The picture <paramref name="bytes" /> hold, scaled down to something a terminal has room for — or
    ///     <see langword="null" /> where they are not a picture this client can read. A file that will not decode is
    ///     not an error worth showing anybody: the post still says what is attached to it, and a reader who cannot see
    ///     the picture is exactly the reader the description is for.
    /// </summary>
    public static Picture? From(byte[] bytes)
    {
        try
        {
            using var image = Image.Load<Rgba32>(bytes);

            if (Math.Max(image.Width, image.Height) > LongestSide)
            {
                // Resized here rather than at the view, which scales by nearest neighbour: a photograph reduced to a
                // tenth of its size that way keeps every tenth pixel and loses every edge in the picture.
                image.Mutate(context => context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(LongestSide, LongestSide),
                }));
            }

            return new Picture(Pixels(image));
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // Anything at all: a truncated download, a format this build has no decoder for, a file that was never an
            // image. There is nothing a reader could do about any of them.
            return null;
        }
    }

    /// <summary>
    ///     The image as the array Terminal.Gui's encoders read: indexed <c>[x, y]</c>, width first.
    /// </summary>
    /// <remarks>
    ///     Alpha is carried through rather than flattened. What becomes of it is the protocol's business — Kitty
    ///     composites it, sixel does where the terminal says it can — and this has no idea what colour the terminal
    ///     behind it is, so any background it chose to flatten onto would be a guess.
    /// </remarks>
    private static Color[,] Pixels(Image<Rgba32> image)
    {
        var pixels = new Color[image.Width, image.Height];

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);

                for (var x = 0; x < row.Length; x++)
                {
                    pixels[x, y] = new Color(row[x].R, row[x].G, row[x].B, row[x].A);
                }
            }
        });

        return pixels;
    }
}
