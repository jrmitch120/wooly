using System.Runtime.CompilerServices;
using Terminal.Gui.Drawing;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Media;

/// <summary>
///     Which part of a picture's box is on the page: the rows from <paramref name="Top" />, and the columns from its
///     left edge. A box straddling the top or bottom of the page is drawn as only the rows still on it.
/// </summary>
/// <param name="Top">The first of the box's rows on the page, counted from the box's own top.</param>
/// <param name="Rows">How many of its rows are on the page.</param>
/// <param name="Columns">How many of its columns are, from the left.</param>
public readonly record struct SixelCrop(int Top, int Rows, int Columns);

/// <summary>A part of a picture as it is handed to the terminal: its pixels, and the same pixels as sixel.</summary>
/// <param name="Pixels">The pixels, the crop's cells in pixels.</param>
/// <param name="Encoded">The sixel.</param>
public sealed record Sixel(Color[,] Pixels, string Encoded);

/// <summary>
///     The sixels of the pictures on the page, encoded once for each part of a box that is drawn and kept (#292).
/// </summary>
/// <remarks>
///     Sixel cannot move an image already on screen, so every scroll step sends each visible picture again — that is
///     the protocol, and the reason a Kitty terminal draws placeholders instead (ADR-0022). What is not the protocol is
///     Terminal.Gui's <c>ImageView</c> scaling and encoding the whole picture again every time its box moves, and the
///     driver encoding a crop again on every frame a box straddles the edge of the page. Here a picture is scaled to
///     its box once, and each crop of it encoded once, so a step costs the sending and nothing else — except the first
///     time a box is cut at a given row, which a box being scrolled past is one step at a time.
/// </remarks>
/// <param name="encode">
///     How pixels become sixel, in so many colours: Terminal.Gui's own encoder, or a test's answer.
/// </param>
/// <param name="elsewhere">
///     Where a crop asked for ahead of time is encoded: off the UI thread in the client, and wherever a test says.
/// </param>
/// <param name="backdrop">
///     What a picture's transparent pixels are laid on before it is encoded — what is behind its box, the band where
///     it is asked with <see langword="true" /> and the page where with <see langword="false" />, where that is known —
///     or <see langword="null" /> where it is not, which leaves them transparent.
/// </param>
internal sealed class SixelPictures(
    Func<Color[,], int, string>? encode = null,
    Action<Action>? elsewhere = null,
    Func<bool, Color?>? backdrop = null)
{
    /// <summary>
    ///     How many crops are kept. A crop is its pixels at four bytes each — a megabyte or so for a photograph across a
    ///     wide page — and its sixel besides, so this is bounded by memory rather than by what might be scrolled back
    ///     to. A page holds a handful of pictures, those at its edges a few crops each as it scrolls and is encoded
    ///     ahead, and every crop on the page is used again each frame, so it is never the one let go of. Custom emoji's
    ///     crops are not counted here but in <see cref="MostCustomEmojiHeld" />.
    /// </summary>
    public const int MostHeld = 32;

    /// <summary>
    ///     How many crops of custom emoji are kept, apart from every other picture's (ADR-0035): a page holds dozens of
    ///     them, and counted with the rest they would let go of the crops a photograph at the page's edge was encoded
    ///     ahead for on every step. A custom emoji's crop is two cells by one, a few kilobytes with its sixel, so this
    ///     is bounded by what a page and the rows either side of it hold rather than by memory.
    /// </summary>
    public const int MostCustomEmojiHeld = 128;

    /// <summary>Whether each picture drawn is opaque, read off its pixels once and kept as long as it is.</summary>
    private static readonly ConditionalWeakTable<Picture, StrongBox<bool>> Opaque = new();

    private readonly Func<Color[,], int, string> _encode = encode ?? Encoded;
    private readonly Func<bool, Color?> _backdrop = backdrop ?? (_ => null);
    private readonly Action<Action> _elsewhere = elsewhere ?? (work => Task.Run(work));
    private readonly Lock _gate = new();
    private readonly Dictionary<Key, LinkedListNode<(Key Key, Sixel Sixel)>> _held = [];

    /// <summary>Every crop held but custom emoji's, the one used last first.</summary>
    private readonly LinkedList<(Key Key, Sixel Sixel)> _used = new();

    /// <summary>Every custom emoji's crop held, the one used last first.</summary>
    private readonly LinkedList<(Key Key, Sixel Sixel)> _usedCustomEmoji = new();
    private readonly Dictionary<Key, TaskCompletionSource<Sixel>> _preparing = [];
    private readonly Dictionary<Scale, Picture> _scaled = [];

    /// <summary>
    ///     <paramref name="crop" /> of <paramref name="picture" /> drawn in <paramref name="inset" />'s box, encoded in
    ///     <paramref name="colours" /> colours and laid on the band where <paramref name="banded" /> says the box is on
    ///     one, and the page where it is not. Encoded here, on the frame asking, only where it was neither asked for
    ///     before nor prepared.
    /// </summary>
    public Sixel Of(Inset inset, Picture picture, CellSize cell, SixelCrop crop, int colours, bool banded = false)
    {
        var key = KeyOf(inset, picture, cell, crop, colours, banded);
        TaskCompletionSource<Sixel>? preparing;

        lock (_gate)
        {
            if (_held.TryGetValue(key, out var known))
            {
                var used = known.List!;

                used.Remove(known);
                used.AddFirst(known);

                return known.Value.Sixel;
            }

            _preparing.TryGetValue(key, out preparing);
        }

        if (preparing is not null)
        {
            try
            {
                return preparing.Task.GetAwaiter().GetResult();
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                // Encoded here instead, as it would have been had nobody prepared it.
            }
        }

        var sixel = Made(key, picture);

        lock (_gate)
        {
            Hold(key, sixel);
        }

        return sixel;
    }

    /// <summary>
    ///     Starts encoding <paramref name="crop" /> elsewhere, for a crop the next step of a scroll will want: a box
    ///     straddling the edge of the page, cut a row higher or lower than it is now.
    /// </summary>
    public void Prepare(Inset inset, Picture picture, CellSize cell, SixelCrop crop, int colours, bool banded = false)
    {
        var key = KeyOf(inset, picture, cell, crop, colours, banded);
        var preparing = new TaskCompletionSource<Sixel>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            if (_held.ContainsKey(key) || !_preparing.TryAdd(key, preparing))
            {
                return;
            }
        }

        _elsewhere(() =>
        {
            // Settled whatever happens, an out-of-memory included: the frame that asks for this crop waits on it, and
            // one left unsettled would leave the UI thread waiting for ever.
            try
            {
                var sixel = Made(key, picture);

                lock (_gate)
                {
                    _preparing.Remove(key);
                    Hold(key, sixel);
                }

                preparing.SetResult(sixel);
            }
            catch (Exception failure)
            {
                lock (_gate)
                {
                    _preparing.Remove(key);
                }

                preparing.SetException(failure);

                if (failure is OutOfMemoryException)
                {
                    throw;
                }
            }
        });
    }

    /// <summary>
    ///     The crop's key, which names a backdrop only for a picture that has anything to lay on one — so that picking a
    ///     post, which moves its rows from the page to the band, encodes again only those of its pictures that show what
    ///     they are on, and never a photograph.
    /// </summary>
    private Key KeyOf(Inset inset, Picture picture, CellSize cell, SixelCrop crop, int colours, bool banded)
    {
        var fill = inset.Fill;

        return new Key(
            new Scale(
                inset.Drawn.Id,
                inset.Columns,
                inset.Rows,
                cell,
                ShowsThrough(picture, fill, inset.Columns * cell.Width, inset.Rows * cell.Height)
                    ? _backdrop(banded)
                    : null,
                DecodedSize.Of(picture),
                fill,
                inset.Drawn.IsCustomEmoji),
            crop,
            colours);
    }

    /// <summary>
    ///     Whether what is behind <paramref name="picture" />'s box shows through it once it takes the box as
    ///     <paramref name="fill" /> says: where it is fitted to a box <paramref name="width" /> by
    ///     <paramref name="height" /> pixels that is not its shape, with room round it, or where it has a pixel less
    ///     than opaque, which is read off its pixels once for each picture (<see cref="Opaque" />), on the frame that
    ///     first wants a cut of it. A square avatar fitted to a box that is square leaves no room, and is encoded once
    ///     whether its post is picked or not.
    /// </summary>
    private static bool ShowsThrough(Picture picture, BoxFill fill, int width, int height) =>
        (fill is BoxFill.Fitted && (long)picture.Width * height != (long)picture.Height * width)
        || !Opaque.GetValue(picture, AllOpaque).Value;

    private static StrongBox<bool> AllOpaque(Picture picture)
    {
        foreach (var pixel in picture.Pixels)
        {
            if (pixel.A != 255)
            {
                return new StrongBox<bool>(false);
            }
        }

        return new StrongBox<bool>(true);
    }

    /// <summary>
    ///     Holds a sixel, letting go of the one used longest ago once there are more than there is room for — among
    ///     custom emoji's crops for one of theirs, and among every other picture's for the rest.
    /// </summary>
    private void Hold(Key key, Sixel sixel)
    {
        if (_held.ContainsKey(key))
        {
            return;
        }

        var (used, most) = key.Scale.CustomEmoji ? (_usedCustomEmoji, MostCustomEmojiHeld) : (_used, MostHeld);

        _held[key] = used.AddFirst((key, sixel));

        while (used.Count > most)
        {
            _held.Remove(used.Last!.Value.Key);
            used.RemoveLast();
        }

        // The scaled pictures go with the last of their crops, so that what is held is bounded by the crops.
        if (_scaled.Count > MostHeld + MostCustomEmojiHeld)
        {
            var wanted = _held.Keys.Select(held => held.Scale).ToHashSet();

            foreach (var gone in _scaled.Keys.Where(scaled => !wanted.Contains(scaled)).ToList())
            {
                _scaled.Remove(gone);
            }
        }
    }

    /// <summary>The crop <paramref name="key" /> names, cut and encoded. Outside the lock: this is the slow part.</summary>
    private Sixel Made(Key key, Picture picture)
    {
        var pixels = Cropped(Scaled(key.Scale, picture), key.Scale.Cell, key.Crop);

        return new Sixel(pixels, _encode(pixels, key.Colours));
    }

    /// <summary>
    ///     Terminal.Gui's own encoder, in at most <paramref name="colours" /> colours chosen from the whole picture
    ///     (<see cref="SixelPalette" />) rather than from its left edge.
    /// </summary>
    private static string Encoded(Color[,] pixels, int colours)
    {
        var (quantized, palette) = SixelPalette.Quantized(pixels, colours);
        var encoder = new SixelEncoder();

        encoder.Quantizer.MaxColors = colours;
        encoder.Quantizer.PaletteBuildingAlgorithm = palette;

        return encoder.EncodeSixel(quantized);
    }

    /// <summary>The rows and columns of <paramref name="crop" />, cut from the box's pixels.</summary>
    private static Color[,] Cropped(Picture box, CellSize cell, SixelCrop crop)
    {
        var top = Math.Min(crop.Top * cell.Height, box.Height);
        var width = Math.Min(crop.Columns * cell.Width, box.Width);
        var height = Math.Min(crop.Rows * cell.Height, box.Height - top);

        if (top == 0 && width == box.Width && height == box.Height)
        {
            return box.Pixels;
        }

        var pixels = new Color[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                pixels[x, y] = box.Pixels[x, top + y];
            }
        }

        return pixels;
    }

    /// <summary>The picture at its box's size in pixels, scaled once — or now and then twice, if two threads race to it.</summary>
    private Picture Scaled(Scale scale, Picture picture)
    {
        lock (_gate)
        {
            if (_scaled.TryGetValue(scale, out var known))
            {
                return known;
            }
        }

        var scaled = Laid(
            PictureDecoder.Scaled(
                picture,
                scale.Columns * scale.Cell.Width,
                scale.Rows * scale.Cell.Height,
                scale.Fill),
            scale.Backdrop);

        lock (_gate)
        {
            _scaled[scale] = scaled;
        }

        return scaled;
    }

    /// <summary>
    ///     <paramref name="picture" /> laid on <paramref name="backdrop" />, so that it has no transparent pixels left.
    /// </summary>
    /// <remarks>
    ///     A sixel with transparent pixels is drawn with the terminal's cells showing through them — and the cells under a
    ///     picture are left unwritten, since that is how the driver knows they are the picture's. So what showed through
    ///     an avatar with a transparent background was whatever text had been on those cells before the page moved. Laid
    ///     on the page, the picture covers its cells whole, as it does through Kitty.
    /// </remarks>
    internal static Picture Laid(Picture picture, Color? backdrop)
    {
        if (backdrop is not { } page)
        {
            return picture;
        }

        var width = picture.Width;
        var height = picture.Height;
        Color[,]? laid = null;

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var pixel = picture.Pixels[x, y];

                if (pixel.A == 255)
                {
                    continue;
                }

                laid ??= (Color[,])picture.Pixels.Clone();

                laid[x, y] = new Color(
                    ((pixel.R * pixel.A) + (page.R * (255 - pixel.A))) / 255,
                    ((pixel.G * pixel.A) + (page.G * (255 - pixel.A))) / 255,
                    ((pixel.B * pixel.A) + (page.B * (255 - pixel.A))) / 255);
            }
        }

        return laid is null ? picture : new Picture(laid);
    }

    /// <summary>
    ///     One picture at one box size, on one size of cell, laid on one backdrop, from pixels decoded at one size
    ///     (<see cref="DecodedSize" />), taking the box as <see cref="BoxFill" /> says
    ///     (<see cref="PictureDecoder.Filled" />) — and whether it is a custom emoji, whose crops are held in
    ///     room of their own (<see cref="MostCustomEmojiHeld" />).
    /// </summary>
    private readonly record struct Scale(
        string Drawn,
        int Columns,
        int Rows,
        CellSize Cell,
        Color? Backdrop,
        DecodedSize Decoded,
        BoxFill Fill,
        bool CustomEmoji);

    /// <summary>One crop of a scaled picture, in so many colours.</summary>
    private readonly record struct Key(Scale Scale, SixelCrop Crop, int Colours);
}
