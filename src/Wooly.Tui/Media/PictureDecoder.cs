using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
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
    ///     How wide a decoded picture may be. A picture is drawn at the full width of the column it is in, which on a
    ///     wide terminal is most of a thousand pixels.
    /// </summary>
    public const int LongestSide = 1024;

    /// <summary>
    ///     How tall a decoded picture may be, which is the cap that does the work: a box is at most
    ///     <see cref="Rendering.Inset.WholeRows" /> rows, and a row is around twenty pixels. Anything taller is decoded
    ///     and held only to be thrown away by the scale down to the box — and at four bytes a pixel, held is the word
    ///     that matters.
    /// </summary>
    public const int TallestSide = 640;

    /// <summary>
    ///     The picture <paramref name="bytes" /> hold, scaled down to something a terminal has room for — or
    ///     <see langword="null" /> where they are not a picture this client can read. A file that will not decode is
    ///     not an error worth showing anybody: the post still says what is attached to it, and a reader who cannot see
    ///     the picture is exactly the reader the description is for.
    /// </summary>
    public static Picture? From(byte[] bytes)
    {
        var room = new Size(LongestSide, TallestSide);

        try
        {
            // The header first, which costs nothing to read, because what it settles is whether the decoder can be
            // told to shrink on the way in: a JPEG can be decoded straight to a fraction of its stored size, so an
            // eight-megapixel photograph never has to exist whole in memory to end up as a few hundred pixels of
            // terminal. Asked for only where the picture is bigger than the room, since a target size is a size to
            // decode *to* and would otherwise blow a thumbnail up to fill it.
            var stored = Image.Identify(bytes).Size;

            var options = stored.Width > room.Width || stored.Height > room.Height
                ? new DecoderOptions { TargetSize = room }
                : new DecoderOptions();

            using var image = Image.Load<Rgba32>(options, bytes);

            // A target size is what the decoder aims at rather than what it promises, so the box is made exact here.
            if (image.Width > room.Width || image.Height > room.Height)
            {
                // Resized here rather than at the view, which scales by nearest neighbour: a photograph reduced to a
                // tenth of its size that way keeps every tenth pixel and loses every edge in the picture.
                image.Mutate(context => context.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = room }));
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
