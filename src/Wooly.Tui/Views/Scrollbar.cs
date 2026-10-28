namespace Wooly.Tui.Views;

/// <summary>
///     Where the <b>Page</b> stands among a screen's rows, as the thumb drawn on the content panel's right edge: its
///     first row down the track and how many rows it is (#500). The track is the rows beside the part of the page that
///     scrolls, so it is as tall as that part.
/// </summary>
/// <param name="Start">How far down the track the thumb begins.</param>
/// <param name="Length">How many rows of the track the thumb covers.</param>
internal readonly record struct Scrollbar(int Start, int Length)
{
    /// <summary>The track: the panel edge's own glyph, so the outline stays whole.</summary>
    public const string Track = "│";

    /// <summary>The thumb, told from the track by its glyph where there is no colour to tell them apart.</summary>
    public const string Thumb = "█";

    /// <summary>The fewest rows a thumb is drawn, so that on a long list it does not shrink to one cell to lose.</summary>
    private const int Least = 2;

    /// <summary>
    ///     The thumb for a page <paramref name="page" /> rows tall whose top is <paramref name="top" /> rows into
    ///     <paramref name="total" /> rows that scroll, or <see langword="null" /> where every row fits and there is no bar.
    /// </summary>
    /// <remarks>
    ///     A page scrolled past the foot, onto the blank under the last thing, is measured as at the foot, so the thumb
    ///     never runs off the end of the track.
    /// </remarks>
    public static Scrollbar? Of(int total, int top, int page)
    {
        if (total <= page || page <= 0)
        {
            return null;
        }

        var track = page;
        var length = Math.Clamp(Rounded((double)track * page / total), Math.Min(Least, track), track);
        var travel = track - length;
        var start = Rounded((double)Math.Clamp(top, 0, total - page) * travel / (total - page));

        return new Scrollbar(Math.Clamp(start, 0, travel), length);
    }

    /// <summary>
    ///     The thumb's maths inverted (#503): the page's top that puts the thumb's first row <paramref name="start" />
    ///     rows down the track, for a page <paramref name="page" /> rows tall over <paramref name="total" /> rows that
    ///     scroll — held at the track's ends, so a thumb dragged past either stops there and the page with it.
    /// </summary>
    /// <remarks>
    ///     Where the page can move further than the thumb can travel, every row of the track is a top the thumb is
    ///     drawn at again; where it can move less, rows of the track share a top, as they must.
    /// </remarks>
    public static int TopFor(int total, int page, int start)
    {
        if (Of(total, 0, page) is not { } bar)
        {
            return 0;
        }

        var travel = page - bar.Length;

        return travel <= 0 ? 0 : Rounded((double)Math.Clamp(start, 0, travel) * (total - page) / travel);
    }

    /// <summary>Whether the track's row <paramref name="row" /> is under the thumb.</summary>
    public bool Covers(int row) => row >= Start && row < Start + Length;

    /// <summary>Which part of the bar the track's row <paramref name="row" /> is: above the thumb, on it, or below it.</summary>
    public ScrollbarPart PartAt(int row) =>
        row < Start ? ScrollbarPart.AboveTheThumb
        : Covers(row) ? ScrollbarPart.Thumb
        : ScrollbarPart.BelowTheThumb;

    private static int Rounded(double rows) => (int)Math.Round(rows, MidpointRounding.AwayFromZero);
}

/// <summary>The parts of a <see cref="Scrollbar" /> the pointer can be on (#502).</summary>
internal enum ScrollbarPart
{
    /// <summary>The track above the thumb, a click on which turns a page up.</summary>
    AboveTheThumb,

    /// <summary>The thumb itself.</summary>
    Thumb,

    /// <summary>The track below the thumb, a click on which turns a page down.</summary>
    BelowTheThumb,
}

/// <summary>
///     The pointer on the <b>Scrollbar</b> as it was last drawn (#502): the bar, and the row of its track the pointer is
///     beside — counted from the track's top, under any pinned rows — whichever of its two columns it is in.
/// </summary>
/// <param name="Bar">The bar as drawn.</param>
/// <param name="Row">How far down the track the pointer is.</param>
internal readonly record struct ScrollbarHit(Scrollbar Bar, int Row)
{
    /// <summary>Which part of the bar the pointer is on.</summary>
    public ScrollbarPart Part => Bar.PartAt(Row);
}

/// <summary>
///     The <b>Scrollbar</b> as a frame settled it (#500): the thumb, how many of the page's rows are pinned above the
///     track, and how many rows scroll beside it — what the thumb was measured against, so that a click on the bar and
///     the thumb's drag answer from the bar as it was drawn.
/// </summary>
/// <param name="Thumb">The thumb as drawn.</param>
/// <param name="Pinned">How many of the page's rows are pinned above the track, which begins under them.</param>
/// <param name="Scrolling">How many rows scroll under the pinned ones, which the thumb measures.</param>
/// <param name="Track">How many rows the track is: the page's rows under the pinned ones.</param>
internal readonly record struct SettledScrollbar(Scrollbar Thumb, int Pinned, int Scrolling, int Track)
{
    /// <summary>
    ///     The bar for <paramref name="rows" /> rows on a page <paramref name="height" /> rows tall whose top is
    ///     <paramref name="top" />, <paramref name="pinned" /> of them pinned above the track — or <see langword="null" />
    ///     where every row that scrolls fits and there is no bar.
    /// </summary>
    public static SettledScrollbar? Of(int rows, int pinned, int top, int height) =>
        Scrollbar.Of(rows - pinned, top, height - pinned) is { } thumb
            ? new SettledScrollbar(thumb, pinned, rows - pinned, height - pinned)
            : null;

    /// <summary>
    ///     The page's top that puts the thumb's row <paramref name="grabbed" /> rows into it beside the track's row
    ///     <paramref name="row" /> (#503) — the grabbed point held inside the thumb as it is now, which rows arriving
    ///     mid-drag may have shortened, and the thumb held at the track's ends.
    /// </summary>
    public int TopWith(int grabbed, int row) =>
        Scrollbar.TopFor(Scrolling, Track, row - Math.Clamp(grabbed, 0, Thumb.Length - 1));
}

/// <summary>
///     The <b>Scrollbar</b>'s thumb taken hold of by a press on it (#503): <see cref="Held" /> while the button is down,
///     then <see cref="LetGo" /> until the click Terminal.Gui makes of the let-go, which is the drag's and not a click on
///     whatever it lands on.
/// </summary>
internal abstract record ThumbGrip
{
    private ThumbGrip()
    {
    }

    /// <summary>
    ///     The thumb held, by its row <paramref name="Grabbed" /> rows from its top — kept rather than where the thumb
    ///     began, so that the pointer drags that point of it however the thumb's length changes under it.
    /// </summary>
    internal sealed record Held(int Grabbed) : ThumbGrip;

    /// <summary>The button let go of the thumb, its click still to come.</summary>
    internal sealed record LetGo : ThumbGrip
    {
        public static LetGo Awaiting { get; } = new();
    }
}
