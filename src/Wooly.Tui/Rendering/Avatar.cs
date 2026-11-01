using Wooly.Tui.Media;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>
///     What an account's face costs the rows it stands beside: the columns they give up for it, the picture to send
///     for, and the box to draw it in once the pixels are here.
/// </summary>
/// <remarks>
///     Nothing at all where this client can already tell no avatar is coming — a terminal offering neither sixel nor
///     the Kitty graphics protocol, or an instance that named no avatar for this account. Columns are too dear to
///     spend holding a space open for a picture that will never fill it (ADR-0016). An avatar that was named and then
///     could not be fetched keeps its columns, because the two answers <see cref="IPictures.Of" /> gives — not here
///     yet, and never coming — are the same answer.
///     <para>
///         Where one <em>is</em> coming the columns are taken from the first frame, before the pixels land, and show
///         the <see cref="StandIn" />'s shade until they do: a byline that gained five columns on arrival would shove
///         the name across the row as the reader was reading it. An attachment's box has been reserved the same way
///         since ADR-0025, from the shape the instance reported; an avatar's is a fixed <see cref="PictureBox" />,
///         and the face is fitted whole inside it and centred, whatever its shape, as a custom emoji is (#532).
///     </para>
///     <para>
///         Said once for the two places that spend them — a post's byline two rows tall (#62) and an account screen's
///         header block four (ADR-0019) — because when the columns are taken and when they are given back is one
///         decision at two sizes, and a second copy of it is how one of them comes to hold a gap open that the other
///         reclaims. How many columns each takes is the cell's to say (<see cref="Columns" />): 4×2 and 8×4 on a cell
///         twice as tall as it is wide.
///     </para>
/// </remarks>
/// <param name="Box">The box the face is drawn in, or <see langword="null" /> where none is drawn.</param>
/// <param name="Gap">How many columns stand between it and the rows beside it.</param>
public readonly record struct Avatar(PictureBox? Box, int Gap)
{
    /// <summary>The avatar to send for, or <see langword="null" /> where none is drawn.</summary>
    public Drawn? Wanted => Box?.Wanted;

    /// <summary>
    ///     What a post's byline spends on its author's face: two rows tall and as near square as whole columns allow
    ///     on this terminal's cell — four wide on a cell twice as tall as it is wide — and one column of air after it
    ///     (#62, #532).
    /// </summary>
    /// <remarks>
    ///     Fixed rather than worked out from the picture's own proportions the way an attachment's box is
    ///     (<see cref="Inset.For" />), because this box is not sized to a picture: it is a slot in a byline, and a
    ///     byline whose height depended on how square somebody's avatar happened to be would be a feed whose rows
    ///     moved as it scrolled. Its width is the font's, which is the same for every row.
    /// </remarks>
    /// <inheritdoc cref="Of" path="/param" />
    public static Avatar Byline(string account, string? address, IPictures? pictures, Raster? raster) =>
        Of(account, address, pictures, raster, BylineRows, gap: 1);

    /// <summary>How tall a byline's box is, and so how many rows an avatar strip takes.</summary>
    internal const int BylineRows = 2;

    /// <summary>
    ///     How wide a byline's box is on <paramref name="raster" />'s cell — and a notification group's avatar strip's
    ///     faces, which are the same faces at the same size (#454).
    /// </summary>
    internal static int BylineColumns(Raster? raster) => Columns(BylineRows, raster);

    /// <summary>
    ///     What an account screen's header block spends on the same face: four rows rather than the byline's two, as
    ///     near square as whole columns allow — eight wide on a cell twice as tall as it is wide — because this is the
    ///     one screen that is about the person rather than about a post (ADR-0019, #532).
    /// </summary>
    /// <inheritdoc cref="Of" path="/param" />
    public static Avatar Header(string account, string? address, IPictures? pictures, Raster? raster) =>
        Of(account, address, pictures, raster, HeaderRows, gap: 2);

    /// <summary>How tall the header's box is, which is the largest an avatar is ever drawn.</summary>
    internal const int HeaderRows = 4;

    /// <summary>
    ///     The most columns a box is given for each of its rows: on a cell three times as tall as it is wide, which no
    ///     font comes near, a box is square at three columns a row.
    /// </summary>
    private const int MostColumnsARow = 3;

    /// <summary>
    ///     The widest the header's box can be on any cell, which is the largest an avatar is ever decoded to
    ///     (<see cref="Drawn.Largest" />): a square avatar is decoded to the header's height, whatever the cell.
    /// </summary>
    internal const int MostHeaderColumns = HeaderRows * MostColumnsARow;

    /// <summary>
    ///     How many columns a box <paramref name="rows" /> tall takes to be nearest square in pixels on
    ///     <paramref name="raster" />'s cell, a half rounded up — or twice its rows where nobody said how big a cell
    ///     is, which is square on the cell twice as tall as it is wide that is assumed then (#532).
    /// </summary>
    /// <remarks>
    ///     The font's to say and never the picture's, so that every avatar on a screen is the same width and nothing
    ///     jumps as they land (GLOSSARY.md's <b>Picture box</b>). What whole columns leave over, at most half a column,
    ///     is the room a fitted avatar is centred in.
    /// </remarks>
    internal static int Columns(int rows, Raster? raster)
    {
        if (raster?.Cell is not { Width: > 0, Height: > 0 } cell)
        {
            return rows * 2;
        }

        var nearest = ((2L * rows * cell.Height) + cell.Width) / (2L * cell.Width);

        return (int)Math.Clamp(nearest, 1, rows * MostColumnsARow);
    }

    /// <summary>How many columns it costs in all: the box and the gap after it, or none.</summary>
    public int Width => Box is { } box ? box.Columns + Gap : 0;

    /// <summary>What <paramref name="account" />'s avatar costs a set of rows <paramref name="rows" /> tall.</summary>
    /// <remarks>
    ///     Private, and the two sizes above are the whole of what may be asked for: a size named at a call site is a
    ///     size that can differ from the one beside it, and where the two boxes stand is the decision this type exists
    ///     to keep in one place.
    /// </remarks>
    /// <param name="account">Whose face it is, as <c>username@instance</c> — what the picture is named by.</param>
    /// <param name="address">Where to fetch it, or <see langword="null" /> where the instance named nowhere.</param>
    /// <param name="pictures">
    ///     Whose pixels have arrived, or <see langword="null" /> for a screen laid out with no terminal in the room.
    /// </param>
    /// <param name="raster">
    ///     How this terminal paints pixels, or <see langword="null" /> where nobody said — which draws no avatar.
    /// </param>
    /// <param name="rows">How tall the box is, which is how many rows stand beside it.</param>
    /// <param name="gap">How many columns to leave between the box and those rows.</param>
    private static Avatar Of(
        string account,
        string? address,
        IPictures? pictures,
        Raster? raster,
        int rows,
        int gap)
    {
        var box = PictureBox.Fixed(
            address is null ? null : Drawn.Avatar(account, address),
            Columns(rows, raster),
            rows,
            pictures,
            raster);

        return box.Wanted is null ? default : new Avatar(box, gap);
    }

    /// <summary><paramref name="rows" /> stepped in to sit beside the avatar, in the order they were given.</summary>
    /// <remarks>
    ///     Asked for the whole set at once rather than a row at a time, so that which row carries the box is settled
    ///     here instead of at each caller — a feed and an account screen laying out different rows is the point, and
    ///     their laying them out differently is what #62 spent its budget preventing.
    ///     <para>
    ///         The box goes on the first row and on none of the rest: a band is named once, at its top.
    ///         <see cref="Line.After" /> is what shifts it along with everything else, so the gutter and the picture
    ///         cannot come to disagree about which column they are in.
    ///     </para>
    /// </remarks>
    public IEnumerable<Line> Across(params Line[] rows)
    {
        // Matched into locals because a lambda inside a struct cannot reach the instance it is written in.
        if (Box is not { Wanted: { } wanted } box)
        {
            return rows;
        }

        // What the box holds on every row it covers — the Stand-in while the face is not here, blank once it is — is
        // the same columns either way, so the name beside it never moves (ADR-0025). Avatars come with no blurhash,
        // so theirs is always the shade.
        var gap = new Span(new string(' ', Gap), Role.Body);

        return rows.Select((row, at) =>
        {
            var beside = row.After(box.Cells(at), gap);

            // The row's own boxes kept beside the avatar's — the name's custom emoji (#524) — and wanted after it.
            return at > 0
                ? beside
                : (beside with { Insets = [.. box.Insets(at, column: 0), .. beside.Insets] }).Wanting(
                    [wanted, .. beside.Wanted]);
        });
    }
}
