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
///         Where one <em>is</em> coming the columns are taken from the first frame, before the pixels land, which is
///         the opposite of what an attachment does: an attachment's box appears under its description and pushes
///         nothing sideways, where a byline that gained five columns on arrival would shove the name across the row as
///         the reader was reading it.
///     </para>
///     <para>
///         Said once for the two places that spend them — a post's byline at 4×2 (#62) and an account screen's header
///         block at 8×4 (ADR-0019) — because when the columns are taken and when they are given back is one decision
///         at two sizes, and a second copy of it is how one of them comes to hold a gap open that the other reclaims.
///     </para>
/// </remarks>
/// <param name="Wanted">The avatar to send for, or <see langword="null" /> where none is drawn.</param>
/// <param name="Box">Where to draw it, or <see langword="null" /> while the pixels are still on their way.</param>
/// <param name="Held">How many columns the box holds.</param>
/// <param name="Gap">How many columns stand between it and the rows beside it.</param>
public readonly record struct Avatar(Drawn? Wanted, Inset? Box, int Held, int Gap)
{
    /// <summary>
    ///     What a post's byline spends on its author's face: four wide and two tall, which is about square in a cell
    ///     twice as tall as it is wide, and one column of air after it (#62).
    /// </summary>
    /// <remarks>
    ///     Fixed rather than worked out from the picture's own proportions the way an attachment's box is
    ///     (<see cref="Inset.For" />), because this box is not sized to a picture: it is a slot in a byline, and a
    ///     byline whose height depended on how square somebody's avatar happened to be would be a feed whose rows
    ///     moved as it scrolled.
    /// </remarks>
    /// <inheritdoc cref="Of" path="/param" />
    public static Avatar Byline(string account, string? address, IPictures? pictures) =>
        Of(account, address, pictures, columns: 4, rows: 2, gap: 1);

    /// <summary>
    ///     What an account screen's header block spends on the same face: twice the byline's box, beside four rows
    ///     rather than two, because this is the one screen that is about the person rather than about a post
    ///     (ADR-0019).
    /// </summary>
    /// <inheritdoc cref="Of" path="/param" />
    public static Avatar Header(string account, string? address, IPictures? pictures) =>
        Of(account, address, pictures, columns: 8, rows: 4, gap: 2);

    /// <summary>How many columns it costs in all: the box and the gap after it, or none.</summary>
    public int Width => Wanted is null ? 0 : Held + Gap;

    /// <summary>What <paramref name="account" />'s avatar costs a set of rows <paramref name="rows" /> tall.</summary>
    /// <remarks>
    ///     Private, and the two sizes above are the whole of what may be asked for: a size named at a call site is a
    ///     size that can differ from the one beside it, and where the two boxes stand is the decision this type exists
    ///     to keep in one place.
    /// </remarks>
    /// <param name="account">Whose face it is, as <c>username@instance</c> — what the picture is named by.</param>
    /// <param name="address">Where to fetch it, or <see langword="null" /> where the instance named nowhere.</param>
    /// <param name="pictures">
    ///     What this terminal can draw and whose pixels have arrived, or <see langword="null" /> for a screen laid out
    ///     with no terminal in the room.
    /// </param>
    /// <param name="columns">How wide the box is.</param>
    /// <param name="rows">How tall it is, which is how many rows stand beside it.</param>
    /// <param name="gap">How many columns to leave between the box and those rows.</param>
    private static Avatar Of(
        string account,
        string? address,
        IPictures? pictures,
        int columns,
        int rows,
        int gap)
    {
        if (pictures?.Cell is null || address is null)
        {
            return default;
        }

        var wanted = Drawn.Avatar(account, address);

        return new Avatar(
            wanted,
            pictures.Of(wanted) is null ? null : new Inset(wanted, Column: 0, columns, rows),
            columns,
            gap);
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
        if (Wanted is not { } wanted)
        {
            return rows;
        }

        // Copied out because a lambda inside a struct cannot reach the instance it is written in.
        var box = Box;
        var held = new string(' ', Held);
        var gap = new string(' ', Gap);

        return rows.Select((row, at) =>
        {
            var beside = row.After(new Span(held, Role.Media), new Span(gap, Role.Body));

            return at > 0 ? beside : beside with { Insets = box is null ? [] : [box], Wants = wanted };
        });
    }
}
