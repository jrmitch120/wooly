using Wooly.Core.Paging;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     How <b>reading on</b> stands for one screen's list (GLOSSARY.md, #460): where the instance said to read on from,
///     whether a read is out for it, and so what the foot of the list says.
/// </summary>
/// <remarks>
///     Kept on the screen rather than in the shell, because it is about the list the screen holds and lasts exactly as
///     long as it does: a refresh is a new screen and starts again at the top, and a screen walked back to has read on
///     as far as it had. A screen whose list does not read on has none at all, which is what keeps every other list
///     from ever being asked.
/// </remarks>
/// <param name="from">Where the first read said to read on from, or <see langword="null" /> where the list is whole.</param>
public sealed class ReadingOn(Onward? from)
{
    /// <summary>What the foot says while a read is in flight.</summary>
    public const string InFlightSays = "reading on…";

    /// <summary>What the foot says once the instance has nothing behind what the list holds.</summary>
    public const string WholeSays = "that's everything";

    /// <summary>What the foot says after a read on failed, until the page next nears the end and tries again (#462).</summary>
    public const string FailedSays = "couldn't read on";

    /// <summary>Where to read on from next, or <see langword="null" /> where the list is whole.</summary>
    public Onward? From { get; private set; } = from;

    /// <summary>Whether the instance has nothing behind what the list holds, so that nothing more is ever asked.</summary>
    public bool IsWhole => From is null;

    /// <summary>Whether a read on is out for this list, which is at most one at a time.</summary>
    public bool InFlight { get; private set; }

    /// <summary>
    ///     Whether the last read on failed (#462). It leaves where to read on from as it was, so the next approach to
    ///     the end — and nothing else, no timer — asks again from the same place.
    /// </summary>
    public bool Failed { get; private set; }

    /// <summary>
    ///     Takes the read on, where there is somewhere to read on from and none is out already — answering where it is
    ///     from — or answers <see langword="null" /> where there is nothing to ask.
    /// </summary>
    internal Onward? Begin()
    {
        if (InFlight || From is not { } from)
        {
            return null;
        }

        InFlight = true;
        Failed = false;

        return from;
    }

    /// <summary>
    ///     The read on is over, however it ended: landed, dropped as stale, or failed — which the enquiry's notice says
    ///     why of, and the foot says until the next one.
    /// </summary>
    internal void Ended(bool failed)
    {
        InFlight = false;
        Failed = failed;
    }

    /// <summary>What a read on that landed says to read on from next, or that the list is now whole.</summary>
    internal void Arrived(Onward? onward) => From = onward;

    /// <summary>
    ///     The muted row under the last thing held, where there is anything to say: a read in flight, a read that
    ///     failed, or a whole list. Nothing between reads, and nothing on a list with nothing on it, which its own
    ///     notice already speaks for.
    /// </summary>
    /// <param name="rows">
    ///     The list's rows, which the foot goes under and never among — none where it holds nothing.
    /// </param>
    public IReadOnlyList<Line> Under(IReadOnlyList<Line> rows) =>
        (InFlight, Failed, IsWhole, holding: rows.Count > 0) switch
        {
            (true, _, _, _) => [.. rows, Line.Of(InFlightSays, Role.Muted)],
            (_, true, _, true) => [.. rows, Line.Of(FailedSays, Role.Muted)],
            (_, _, true, true) => [.. rows, Line.Of(WholeSays, Role.Muted)],
            _ => rows,
        };
}
