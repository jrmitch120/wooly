using Wooly.Core.Posts;
using Wooly.Tui.Media;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A picture set into a post: which attachment it is, and the box of cells it gets. Media is drawn in place inside
///     a feed item or a post rather than on a screen of its own (<c>docs/tui-shell.md</c>), so a picture is part of the
///     rows a post produces and scrolls with them.
/// </summary>
/// <remarks>
///     A box exists only where there is a picture to put in it and a terminal able to draw one. There is no
///     cell-by-cell fallback: a photograph reduced to one coloured block per cell is not a picture of anything, and an
///     attachment a terminal cannot draw reads better as the link and description the CLI gives it (ADR-0016).
/// </remarks>
/// <param name="Media">The attachment to draw.</param>
/// <param name="Column">How far in from the left of the row the box starts.</param>
/// <param name="Columns">How wide the box is.</param>
/// <param name="Rows">How tall the box is, counting the row this inset is on.</param>
public sealed record Inset(PostMedia Media, int Column, int Columns, int Rows)
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
    ///     The box <paramref name="picture" /> gets: the full width it is allowed, at the picture's own proportions,
    ///     shrunk to fit where that would be taller than <paramref name="mostRows" />.
    /// </summary>
    /// <remarks>
    ///     Width-driven rather than height-driven, which is the whole difference between an inline picture and a
    ///     thumbnail: a reader looking at a photograph in a terminal wants it as large as the column it is in allows,
    ///     and the height that follows from its proportions is what it costs. The cap is what stops a tall picture
    ///     taking a screen and a half of a feed.
    /// </remarks>
    /// <param name="media">The attachment being drawn.</param>
    /// <param name="picture">Its pixels, whose proportions settle the shape of the box.</param>
    /// <param name="cell">How many pixels one cell is, which is what turns those proportions into rows and columns.</param>
    /// <param name="width">How many columns there are to draw in.</param>
    /// <param name="mostRows">The most rows this box may take.</param>
    /// <returns>The box, or <see langword="null" /> where there is no room for one.</returns>
    public static Inset? For(PostMedia media, Picture picture, CellSize cell, int width, int mostRows)
    {
        if (width < 1 || mostRows < 1 || picture.Width < 1 || picture.Height < 1
            || cell.Width < 1 || cell.Height < 1)
        {
            return null;
        }

        var columns = width;
        var rows = Rounded((long)columns * cell.Width * picture.Height, (long)picture.Width * cell.Height);

        if (rows > mostRows)
        {
            // Too tall at full width, so the height is what is fixed and the width follows from it.
            rows = mostRows;
            columns = Rounded((long)rows * cell.Height * picture.Width, (long)picture.Height * cell.Width);
            columns = Math.Clamp(columns, 1, width);
        }

        return new Inset(media, Column: 0, Columns: columns, Rows: Math.Max(1, rows));
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
