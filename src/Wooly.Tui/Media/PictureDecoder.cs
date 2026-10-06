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
    ///     How wide a decoded picture may be where nothing has said how wide the window is: most of a thousand pixels,
    ///     which is a wide terminal's column.
    /// </summary>
    public const int LongestSide = 1024;

    /// <summary>
    ///     How tall a decoded picture may be where nothing has said how big a cell is: about
    ///     <see cref="Rendering.Inset.WholeRows" /> rows of twenty pixels.
    /// </summary>
    public const int TallestSide = 640;

    /// <summary>
    ///     The room a picture is decoded into where nothing better is known (<see cref="LongestSide" /> by
    ///     <see cref="TallestSide" />). <see cref="Pictures" /> knows better once the terminal has said how big it is,
    ///     and decodes into the largest box the window could draw instead (ADR-0025).
    /// </summary>
    public static readonly Size SomeRoom = new(LongestSide, TallestSide);

    /// <summary>
    ///     The picture <paramref name="bytes" /> hold, scaled down to <see cref="SomeRoom" /> — or
    ///     <see langword="null" /> where they are not a picture this client can read.
    /// </summary>
    public static Picture? From(byte[] bytes) => From(bytes, SomeRoom);

    /// <summary>
    ///     The picture <paramref name="bytes" /> hold, scaled down to fit inside <paramref name="room" /> — or
    ///     <see langword="null" /> where they are not a picture this client can read. A file that will not decode is
    ///     not an error worth showing anybody: the post still says what is attached to it, and a reader who cannot see
    ///     the picture is exactly the reader the description is for.
    /// </summary>
    /// <remarks>
    ///     Held at exactly <see cref="Fitted" /> of its stored size, not merely near it, because that size is how
    ///     <see cref="Pictures" /> tells a picture held for a smaller window from one already as sharp as this window
    ///     could draw it, and a pixel of difference would read as the former on every frame.
    /// </remarks>
    public static Picture? From(byte[] bytes, Size room)
    {
        try
        {
            // The header first, which costs nothing to read, because what it settles is whether the decoder can be
            // told to shrink on the way in: a JPEG can be decoded straight to a fraction of its stored size, so an
            // eight-megapixel photograph never has to exist whole in memory to end up as a few hundred pixels of
            // terminal. Asked for only where the picture is bigger than the room, since a target size is a size to
            // decode *to* and would otherwise blow a thumbnail up to fill it.
            var stored = Image.Identify(bytes).Size;
            var fitted = Fitted(stored, room);

            var options = fitted != stored
                ? new DecoderOptions { TargetSize = fitted }
                : new DecoderOptions();

            using var image = Image.Load<Rgba32>(options, bytes);

            // A target size is what the decoder aims at rather than what it promises, so the size is made exact here.
            if (image.Size != fitted)
            {
                // Resized here rather than at the view, which scales by nearest neighbour: a photograph reduced to a
                // tenth of its size that way keeps every tenth pixel and loses every edge in the picture.
                image.Mutate(context => context.Resize(fitted.Width, fitted.Height));
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
    ///     How big the picture in <paramref name="bytes" /> is stored, read from its header alone, or
    ///     <see langword="null" /> where they are not a picture this client can read.
    /// </summary>
    public static Size? Measured(byte[] bytes)
    {
        try
        {
            return Image.Identify(bytes).Size;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>
    ///     How big a picture stored at <paramref name="stored" /> is once it is decoded into <paramref name="room" />:
    ///     as it is where it fits, and otherwise scaled down in its own proportions until it does — never up, since a
    ///     thumbnail blown up to fill the room is the same thumbnail at four times the memory.
    /// </summary>
    public static Size Fitted(Size stored, Size room)
    {
        if (stored.Width <= room.Width && stored.Height <= room.Height)
        {
            return stored;
        }

        // Whichever side is the tighter fit settles the scale; the other follows from it. Multiplied out in long, for
        // the same reason Inset rounds that way: a large picture's sides multiplied together overflow an int.
        if ((long)stored.Width * room.Height >= (long)stored.Height * room.Width)
        {
            return new Size(room.Width, (int)Math.Max(1, (long)stored.Height * room.Width / stored.Width));
        }

        return new Size((int)Math.Max(1, (long)stored.Width * room.Height / stored.Height), room.Height);
    }

    /// <summary>
    ///     <paramref name="picture" /> resized to exactly <paramref name="width" /> by <paramref name="height" />
    ///     pixels — the box it is drawn in, so that the terminal is handed pixels it has nothing left to do to.
    /// </summary>
    public static Picture Scaled(Picture picture, int width, int height)
    {
        if (picture.Width == width && picture.Height == height)
        {
            return picture;
        }

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

        image.Mutate(context => context.Resize(width, height));

        return new Picture(Pixels(image));
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
