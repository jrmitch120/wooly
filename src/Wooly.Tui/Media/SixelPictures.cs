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
///     <para>
///         The picture at its box's size is not held here but in the <see cref="BoxPictures" /> Kitty through a box
///         is handed from too, with nothing laid under it; each crop is cut from it and laid on what is behind its box
///         once it is cut (#554). So picking a post, which moves its rows from the page to the band, cuts its
///         see-through pictures again for the band without scaling them again.
///     </para>
/// </remarks>
internal sealed class SixelPictures
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

    private readonly Func<Color[,], int, string> _encode;
    private readonly Func<bool, Color?> _backdrop;
    private readonly PreparedAhead<Key, (Inset Inset, Picture Picture), Sixel> _held;

    /// <param name="encode">
    ///     How pixels become sixel, in so many colours: Terminal.Gui's own encoder, or a test's answer.
    /// </param>
    /// <param name="elsewhere">
    ///     Where a crop asked for ahead of time is encoded: off the UI thread in the client, and wherever a test says.
    /// </param>
    /// <param name="backdrop">
    ///     What a picture's transparent pixels are laid on before it is encoded — what is behind its box, the band
    ///     where it is asked with <see langword="true" /> and the page where with <see langword="false" />, where that
    ///     is known — or <see langword="null" /> where it is not, which leaves them transparent.
    /// </param>
    /// <param name="boxes">
    ///     Where the crops are cut from: the box pictures <see cref="Placing" /> shares with Kitty through a box,
    ///     or where none is said, some of this one's own.
    /// </param>
    public SixelPictures(
        Func<Color[,], int, string>? encode = null,
        Action<Action>? elsewhere = null,
        Func<bool, Color?>? backdrop = null,
        BoxPictures? boxes = null)
    {
        _encode = encode ?? Encoded;
        _backdrop = backdrop ?? (_ => null);
        Boxes = boxes ?? new BoxPictures(elsewhere);
        _held = new PreparedAhead<Key, (Inset Inset, Picture Picture), Sixel>(
            (key, from) => Made(key, from.Inset, from.Picture),
            elsewhere ?? (work => Task.Run(work)),
            new LeastRecentlyUsed<Key>(MostHeld, key => key.CustomEmoji, MostCustomEmojiHeld));
    }

    /// <summary>The box pictures the crops are cut from, which Kitty through a box is handed from too.</summary>
    public BoxPictures Boxes { get; }

    /// <summary>
    ///     <paramref name="crop" /> of <paramref name="picture" /> drawn in <paramref name="inset" />'s box, encoded in
    ///     <paramref name="colours" /> colours and laid on the band where <paramref name="banded" /> says the box is on
    ///     one, and the page where it is not. Encoded here, on the frame asking, only where it was neither asked for
    ///     before nor prepared. On the UI thread.
    /// </summary>
    public Sixel Of(Inset inset, Picture picture, CellSize cell, SixelCrop crop, int colours, bool banded = false)
    {
        // Asked for even where the crop is held, so that the box picture stays for the next step's crop rather than
        // being let go of and scaled again for it.
        Boxes.Ask(inset, picture, cell);

        return _held.Of(KeyOf(inset, picture, cell, crop, colours, banded), (inset, picture));
    }

    /// <summary>
    ///     Starts encoding <paramref name="crop" /> elsewhere, for a crop the next step of a scroll will want: a box
    ///     straddling the edge of the page, cut a row higher or lower than it is now. On the UI thread.
    /// </summary>
    public void Prepare(Inset inset, Picture picture, CellSize cell, SixelCrop crop, int colours, bool banded = false)
    {
        Boxes.Ask(inset, picture, cell);
        _held.Prepare(KeyOf(inset, picture, cell, crop, colours, banded), (inset, picture));
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
            inset.Drawn.Id,
            inset.Columns,
            inset.Rows,
            cell,
            ShowsThrough(picture, fill, inset.Columns * cell.Width, inset.Rows * cell.Height)
                ? _backdrop(banded)
                : null,
            DecodedSize.Of(picture),
            fill,
            inset.Drawn.IsCustomEmoji,
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
    ///     The crop <paramref name="key" /> names, cut from the box picture and laid on its backdrop, and encoded:
    ///     wherever it was asked for, on the frame or elsewhere. Outside the lock: this is the slow part.
    /// </summary>
    private Sixel Made(Key key, Inset inset, Picture picture)
    {
        var pixels = Cropped(Boxes.Unasked(inset, picture, key.Cell), key.Cell, key.Crop, key.Backdrop);

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

    /// <summary>
    ///     The rows and columns of <paramref name="crop" />, cut from the box's pixels and laid on
    ///     <paramref name="backdrop" /> as they are copied — the very pixels <see cref="Laid" /> gives the cut, since
    ///     laying a pixel reads only that pixel, without copying the cut a second time to lay it. The box itself is
    ///     shared with Kitty through a box, and never laid where it is.
    /// </summary>
    private static Color[,] Cropped(Picture box, CellSize cell, SixelCrop crop, Color? backdrop)
    {
        var top = Math.Min(crop.Top * cell.Height, box.Height);
        var width = Math.Min(crop.Columns * cell.Width, box.Width);
        var height = Math.Min(crop.Rows * cell.Height, box.Height - top);

        if (top == 0 && width == box.Width && height == box.Height)
        {
            return Laid(box, backdrop).Pixels;
        }

        var pixels = new Color[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var pixel = box.Pixels[x, top + y];

                pixels[x, y] = backdrop is { } behind ? On(pixel, behind) : pixel;
            }
        }

        return pixels;
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
                laid[x, y] = On(pixel, page);
            }
        }

        return laid is null ? picture : new Picture(laid);
    }

    /// <summary><paramref name="pixel" /> laid on <paramref name="backdrop" />: itself where it is opaque.</summary>
    private static Color On(Color pixel, Color backdrop) =>
        pixel.A == 255
            ? pixel
            : new Color(
                ((pixel.R * pixel.A) + (backdrop.R * (255 - pixel.A))) / 255,
                ((pixel.G * pixel.A) + (backdrop.G * (255 - pixel.A))) / 255,
                ((pixel.B * pixel.A) + (backdrop.B * (255 - pixel.A))) / 255);

    /// <summary>
    ///     One crop of one picture at one box size, on one size of cell, laid on one backdrop and encoded in so many
    ///     colours, from pixels decoded at one size (<see cref="DecodedSize" />), taking the box as
    ///     <see cref="BoxFill" /> says (<see cref="PictureDecoder.Filled" />) — and whether it is a custom emoji, whose
    ///     crops are held in room of their own (<see cref="MostCustomEmojiHeld" />).
    /// </summary>
    private readonly record struct Key(
        string Drawn,
        int Columns,
        int Rows,
        CellSize Cell,
        Color? Backdrop,
        DecodedSize Decoded,
        BoxFill Fill,
        bool CustomEmoji,
        SixelCrop Crop,
        int Colours);
}
