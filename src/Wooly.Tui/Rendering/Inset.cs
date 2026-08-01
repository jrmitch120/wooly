using Wooly.Core.Posts;

namespace Wooly.Tui.Rendering;

/// <summary>
///     A picture set into a post: which attachment it is, and the box of cells it gets. Media is drawn in place inside
///     a feed item or a post rather than on a screen of its own (<c>docs/tui-shell.md</c>), so a picture is part of the
///     rows a post produces and scrolls with them.
/// </summary>
/// <remarks>
///     What is named here is the <em>attachment</em>, not the pixels. The box is settled while the picture is still
///     being fetched — or has failed to arrive at all — which is what keeps a feed from jumping under a reader as
///     images land one by one (ADR-0016). Whether anything is drawn in the box is the view's question, and it is asked
///     on every frame.
/// </remarks>
/// <param name="Media">The attachment to draw.</param>
/// <param name="Column">How far in from the left of the row the box starts.</param>
/// <param name="Columns">How wide the box is.</param>
/// <param name="Rows">How tall the box is, counting the row this inset is on.</param>
public sealed record Inset(PostMedia Media, int Column, int Columns, int Rows)
{
    /// <summary>
    ///     How tall a picture is in a feed item, where it is one post among many and the text is what a reader is
    ///     scanning.
    /// </summary>
    public const int FeedRows = 6;

    /// <summary>
    ///     How tall a picture is on the post screen, where the post is the whole of what is being looked at and a
    ///     reader who drilled into it has said which post they care about.
    /// </summary>
    public const int WholeRows = 12;

    /// <summary>
    ///     A post's pictures laid out side by side across one band of rows, in the order their author attached them.
    ///     Side by side rather than stacked because Mastodon allows four attachments and four bands would bury the post
    ///     that carries them — the same arrangement, and the same reason, as every other client's grid.
    /// </summary>
    /// <param name="pictures">The attachments to draw, which are the ones a terminal can draw at all.</param>
    /// <param name="width">How many columns the band has, which at an 80-column terminal is 61 less any gutter.</param>
    /// <param name="rows">How tall the band is.</param>
    /// <returns>One inset per picture, or nothing at all where there is not the room for even a column each.</returns>
    public static IReadOnlyList<Inset> Across(IReadOnlyList<PostMedia> pictures, int width, int rows)
    {
        if (pictures.Count == 0 || rows < 1)
        {
            return [];
        }

        // A cell is about twice as tall as it is wide, so a box this many rows tall holds about twice as many pixel
        // rows — and a picture of the shape most photographs are wants around half as many again across. Nothing here
        // has to be exact: the picture keeps its own proportions inside whatever box it is given, and the slack shows
        // as a margin rather than as a stretched face.
        var wanted = rows * 3;

        // One blank column between boxes, so two pictures side by side read as two rather than as one wide one.
        var room = (width - (pictures.Count - 1)) / pictures.Count;
        var columns = Math.Min(wanted, room);

        if (columns < 1)
        {
            return [];
        }

        return [.. pictures.Select((picture, at) => new Inset(picture, at * (columns + 1), columns, rows))];
    }

    /// <summary>How wide the whole band is, which is what the row standing in for it has to measure.</summary>
    public static int Width(IReadOnlyList<Inset> insets) =>
        insets.Count == 0 ? 0 : insets[^1].Column + insets[^1].Columns;

    /// <summary>This inset moved <paramref name="by" /> columns to the right, for a row something was put in front of.</summary>
    public Inset ShiftedBy(int by) => this with { Column = Column + by };
}
