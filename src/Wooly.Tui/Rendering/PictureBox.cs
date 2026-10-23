using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     The cells a <b>drawn</b> picture is painted in, settled before its pixels arrive so that nothing moves when they
///     come or go (GLOSSARY.md's <b>Picture box</b>): what every row of it holds, and what is set into each row to be
///     drawn — the <b>Stand-in</b>'s shade and blur until the pixels are here, and the picture once they are.
/// </summary>
/// <remarks>
///     Said once for every box a screen holds, rather than once at each, because "shade until the pixels are here, then
///     the picture in the box" was four copies of itself (#438) — a post's picture and a link preview's, an avatar, a
///     pending attachment's row and the public profile form — and a copy is how one of them comes to hold its shade
///     for good where the rest would draw nothing. Callers decide only where the box sits: beside a name, at a row's
///     column, across the full width, beside a label.
///     <para>
///         Two kinds. A <see cref="Fixed" /> box is filled: a picture of another shape is cropped to it from the middle
///         by the raster paths (<see cref="PictureDecoder.Filled" />), never stretched, so the box is the inset. A
///         <see cref="Shaped" /> box is the shape the instance reported, which can be a guess, so the picture is fitted
///         inside it and centred, with slack where the guess was wrong (ADR-0025).
///     </para>
/// </remarks>
public sealed record PictureBox
{
    /// <summary>What every row of the box holds but the one <see cref="_set" /> is on.</summary>
    private readonly Span _held;

    /// <summary>What the row <see cref="_set" /> is on holds.</summary>
    private readonly Span _heldWhereSet;

    /// <summary>
    ///     What is set into the box, at its own left — the blur or the picture — or <see langword="null" /> where
    ///     nothing is.
    /// </summary>
    private readonly Inset? _set;

    /// <summary>Which row of the box <see cref="_set" /> is on.</summary>
    private readonly int _down;

    private PictureBox(Drawn? wanted, int columns, int rows, Span held, Inset? set = null, int down = 0)
        : this(wanted, columns, rows, held, held, set, down)
    {
    }

    private PictureBox(Drawn? wanted, int columns, int rows, Span held, Span heldWhereSet, Inset? set, int down)
    {
        Wanted = wanted;
        Columns = columns;
        Rows = rows;
        _held = held;
        _heldWhereSet = heldWhereSet;
        _set = set;
        _down = down;
    }

    /// <summary>The picture to send for, or <see langword="null" /> where none is drawn.</summary>
    public Drawn? Wanted { get; }

    /// <summary>How wide the box is.</summary>
    public int Columns { get; }

    /// <summary>How tall the box is.</summary>
    public int Rows { get; }

    /// <summary>
    ///     A box <paramref name="columns" /> by <paramref name="rows" /> for <paramref name="wanted" />, the same size
    ///     whatever the picture's own shape, and filled by it once it is here: an avatar, a pending attachment's
    ///     picture, the public profile form's avatar and header.
    /// </summary>
    /// <param name="wanted">The picture, or <see langword="null" /> where there is none to draw.</param>
    /// <param name="columns">How wide the box is.</param>
    /// <param name="rows">How tall it is.</param>
    /// <param name="pictures">
    ///     Whose pixels have arrived, or <see langword="null" /> for a screen laid out with no terminal in the room.
    /// </param>
    /// <param name="raster">How this terminal paints pixels, or <see langword="null" /> where nobody said.</param>
    /// <returns>The box, which is blank columns wanting nothing where no picture is drawn.</returns>
    public static PictureBox Fixed(Drawn? wanted, int columns, int rows, IPictures? pictures, Raster? raster)
    {
        if (wanted is null || !Drawing.Draws(pictures, raster, out _))
        {
            return new PictureBox(null, columns, rows, Blanks(columns, Role.Body));
        }

        return pictures.Of(wanted) is null
            ? new PictureBox(wanted, columns, rows, StandIn.Row(columns))
            : new PictureBox(wanted, columns, rows, Blanks(columns), new Inset(wanted, Column: 0, columns, rows));
    }

    /// <summary>
    ///     A box the shape the instance reported for <paramref name="drawn" />, at the full <paramref name="width" />
    ///     and no taller than <paramref name="mostRows" /> (<see cref="Inset.For" />), with the picture fitted inside
    ///     it and centred once it is here (<see cref="Inset.Fit" />): a post's picture, a link preview's.
    /// </summary>
    /// <param name="drawn">The picture.</param>
    /// <param name="shape">
    ///     The shape the instance reported for it, or <see langword="null" /> where it reported none.
    /// </param>
    /// <param name="blurhash">
    ///     The instance's blurhash of the picture, set into the top row over the shade while the picture is not here,
    ///     or <see langword="null" /> where it sent none (#349).
    /// </param>
    /// <param name="width">How many columns there are to draw in.</param>
    /// <param name="mostRows">The most rows the box may take.</param>
    /// <param name="pictures">Whose pixels have arrived, if anywhere's.</param>
    /// <param name="raster">How this terminal paints pixels, if anybody said.</param>
    /// <param name="blurs">What decodes the blurhash, or <see langword="null" /> where no blur is drawn.</param>
    /// <returns>The box, or <see langword="null" /> where no picture is drawn or there is no room for one.</returns>
    public static PictureBox? Shaped(
        Drawn drawn,
        PictureShape? shape,
        string? blurhash,
        int width,
        int mostRows,
        IPictures? pictures,
        Raster? raster,
        Blurs? blurs)
    {
        if (!Drawing.Draws(pictures, raster, out var cell)
            || Inset.For(drawn, shape, cell, width, mostRows) is not { } box)
        {
            return null;
        }

        if (pictures.Of(drawn) is not { } picture)
        {
            // The blur is set into the whole box over the shade rather than in place of it: the shade is what shows
            // until the blur is encoded and drawn, and what shows where it never decodes at all (#349).
            return new PictureBox(
                drawn,
                box.Columns,
                box.Rows,
                StandIn.Row(box.Columns),
                StandIn.WithBlur(box, blurhash, blurs));
        }

        var (fitted, down) = box.Fit(picture, cell);

        // The row the picture is on runs to the picture's right edge and no further, and the rest are the page: a row
        // carrying an inset measures it, so what scrolls and what is drawn cannot come apart.
        return new PictureBox(
            drawn,
            box.Columns,
            box.Rows,
            Blanks(0, Role.Body),
            Blanks(fitted.Column + fitted.Columns),
            fitted,
            down);
    }

    /// <summary>
    ///     What row <paramref name="row" /> of the box holds: the Stand-in's shade, the box's width across, while a
    ///     picture is coming and not here; blank columns where none is drawn or a fixed box's picture is here; and where
    ///     a shaped box's picture is here, blanks to the picture's right edge on the row it is set into and nothing on
    ///     the rest.
    /// </summary>
    public Span Holds(int row) => _set is not null && row == _down ? _heldWhereSet : _held;

    /// <summary>
    ///     What is set into row <paramref name="row" /> of the box, where the box starts <paramref name="column" />
    ///     columns into the row: on one row at most, and on none while there is nothing to draw.
    /// </summary>
    public IReadOnlyList<Inset> On(int row, int column) =>
        _set is { } set && row == _down ? [set.ShiftedBy(column)] : [];

    private static Span Blanks(int columns, Role role = Role.Media) => new(new string(' ', Math.Max(0, columns)), role);
}
