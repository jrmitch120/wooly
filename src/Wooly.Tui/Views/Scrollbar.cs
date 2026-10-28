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

    /// <summary>Whether the track's row <paramref name="row" /> is under the thumb.</summary>
    public bool Covers(int row) => row >= Start && row < Start + Length;

    private static int Rounded(double rows) => (int)Math.Round(rows, MidpointRounding.AwayFromZero);
}
