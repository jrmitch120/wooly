using Wooly.Core.Posts;
using Wooly.Tui.Media;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A picture set into a post: which attachment it is, and the box of cells it gets. Media is drawn in place inside
///     a feed item or a post rather than on a screen of its own (<c>docs/tui-shell.md</c>), so a picture is part of the
///     rows a post produces and scrolls with them.
/// </summary>
/// <remarks>
///     An inset is where pixels are put, so a row carries one only once they are here; the box they go in is reserved
///     before that, from the shape the instance reported, and shows its <b>Stand-in</b> until they arrive (ADR-0025).
///     A box exists only where there is a picture coming and a terminal able to draw one. There is no
///     cell-by-cell fallback: a photograph reduced to one coloured block per cell is not a picture of anything, and an
///     attachment a terminal cannot draw reads better as the link and description the CLI gives it (ADR-0016).
/// </remarks>
/// <param name="Drawn">The picture to draw.</param>
/// <param name="Column">How far in from the left of the row the box starts.</param>
/// <param name="Columns">How wide the box is.</param>
/// <param name="Rows">How tall the box is, counting the row this inset is on.</param>
public sealed record Inset(Drawn Drawn, int Column, int Columns, int Rows)
{
    /// <summary>
    ///     The most rows a picture takes in a feed item, where it is one post among many and the reader is scanning
    ///     rather than looking.
    /// </summary>
    public const int FeedRows = 16;

    /// <summary>
    ///     The most rows a picture takes on the post screen, where the post is the whole of what is on screen and a
    ///     reader who pressed <c>⏎</c> has said which post they care about.
    /// </summary>
    public const int WholeRows = 32;

    /// <summary>
    ///     The pixels of a <b>Stand-in</b>'s blur, already in hand, where this inset is one (<see cref="StandIn.Blurred" />)
    ///     — or <see langword="null" /> for a picture, whose pixels are looked up from <see cref="IPictures" />.
    /// </summary>
    /// <remarks>
    ///     Carried on the inset rather than looked up, because a blur is decoded from the post itself and never fetched:
    ///     keeping it out of the picture cache is what stops a screen of blurs crowding out the pictures they stand in
    ///     for (#349, ADR-0025).
    /// </remarks>
    public Picture? Blurred { get; init; }

    /// <summary>
    ///     What a box is reserved at where the instance reported no shape: sixteen wide by nine tall, the shape most
    ///     photographs and video frames are nearest to, so that even then a guess is a box and not a jump (ADR-0025).
    /// </summary>
    public static readonly PictureShape Unsaid = new(16, 9);

    /// <summary>
    ///     The box a picture of <paramref name="shape" /> gets: the full width it is allowed, at the shape's
    ///     proportions, shrunk to fit where that would be taller than <paramref name="mostRows" /> — and
    ///     <see cref="Unsaid" />'s where the instance reported none.
    /// </summary>
    /// <remarks>
    ///     Width-driven rather than height-driven, which is the whole difference between an inline picture and a
    ///     thumbnail: a reader looking at a photograph in a terminal wants it as large as the column it is in allows,
    ///     and the height that follows from its proportions is what it costs. The cap is what stops a tall picture
    ///     taking a screen and a half of a feed.
    ///     <para>
    ///         From the shape the instance reported rather than from the pixels, so the box is settled before they
    ///         arrive and does not change size when they do, or when they are let go of (ADR-0025, superseding
    ///         ADR-0016's sizing from the picture's own proportions). Pixels of another shape are fitted inside it
    ///         instead (<see cref="Fit" />).
    ///     </para>
    /// </remarks>
    /// <param name="drawn">The picture being drawn.</param>
    /// <param name="shape">The shape the instance reported for it, or <see langword="null" /> where it reported none.</param>
    /// <param name="cell">How many pixels one cell is, which is what turns those proportions into rows and columns.</param>
    /// <param name="width">How many columns there are to draw in.</param>
    /// <param name="mostRows">The most rows this box may take.</param>
    /// <returns>The box, or <see langword="null" /> where there is no room for one.</returns>
    public static Inset? For(Drawn drawn, PictureShape? shape, CellSize cell, int width, int mostRows)
    {
        var (wide, tall) = shape ?? Unsaid;

        return Sized(drawn, wide, tall, cell, width, mostRows);
    }

    /// <summary>
    ///     Where <paramref name="picture" /> goes inside this box: as large as fits without squashing it, centred, with
    ///     the page around it where its proportions are not the box's. The box itself is unchanged, which is the point:
    ///     a shape reported wrongly, or the 16:9 guessed where none was, costs a strip of space rather than a jump
    ///     (ADR-0025).
    /// </summary>
    /// <returns>
    ///     The picture's own inset, at <see cref="Column" /> plus how far in it is centred, and how many rows down the
    ///     box its top is.
    /// </returns>
    public (Inset Inset, int Row) Fit(Picture picture, CellSize cell)
    {
        if (Sized(Drawn, picture.Width, picture.Height, cell, Columns, Rows) is not { } fitted)
        {
            return (this, 0);
        }

        return (fitted with { Column = Column + ((Columns - fitted.Columns) / 2) }, (Rows - fitted.Rows) / 2);
    }

    /// <summary>
    ///     The box a <paramref name="wide" /> by <paramref name="tall" /> picture gets: full width, at those
    ///     proportions, narrowed to match where that is taller than <paramref name="mostRows" />.
    /// </summary>
    private static Inset? Sized(Drawn drawn, int wide, int tall, CellSize cell, int width, int mostRows)
    {
        if (width < 1 || mostRows < 1 || wide < 1 || tall < 1 || cell.Width < 1 || cell.Height < 1)
        {
            return null;
        }

        var columns = width;
        var rows = Rounded((long)columns * cell.Width * tall, (long)wide * cell.Height);

        if (rows > mostRows)
        {
            // Too tall at full width, so the height is what is fixed and the width follows from it.
            rows = mostRows;
            columns = Rounded((long)rows * cell.Height * wide, (long)tall * cell.Width);
            columns = Math.Clamp(columns, 1, width);
        }

        return new Inset(drawn, Column: 0, Columns: columns, Rows: Math.Max(1, rows));
    }

    /// <summary>How wide the whole band is, which is what the row standing in for it has to measure.</summary>
    public static int Width(IReadOnlyList<Inset> insets) =>
        insets.Count == 0 ? 0 : insets[^1].Column + insets[^1].Columns;

    /// <summary>This inset moved <paramref name="by" /> columns to the right, for a row something was put in front of.</summary>
    public Inset ShiftedBy(int by) => this with { Column = Column + by };

    /// <summary>
    ///     <paramref name="value" /> over <paramref name="by" />, rounded to nearest and never less than one. Worked
    ///     out in <see cref="long" /> because the pixel counts on both sides are multiplied together first, and a large
    ///     picture on a wide terminal overflows an <see cref="int" /> before the division brings it back down.
    /// </summary>
    private static int Rounded(long value, long by) => by < 1 ? 1 : (int)Math.Max(1, (value + (by / 2)) / by);
}
