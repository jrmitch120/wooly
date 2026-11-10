using System.Drawing;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Views;

/// <summary>
///     What the shell window's last click was spent on, which the double click it turns out to be the first half of is
///     told by: Terminal.Gui reports the pair's first click on its own before the pair, so what that click did is kept
///     until the pair comes. Replaced whole by every click, so that nothing one click did outlives the next, and spent
///     whole by the double click, so that a later pair is not swallowed by what its own first click never saw (#592).
/// </summary>
internal sealed record LastClick
{
    /// <summary>A click spent on nothing a double click needs to know of, and what there is before any click.</summary>
    public static LastClick Nothing { get; } = new();

    /// <summary>
    ///     Whether it was spent declining an open question, which spends the double click it turns out to be the first
    ///     half of (#291).
    /// </summary>
    public bool DeclinedQuestion { get; init; }

    /// <summary>
    ///     Whether it was spent toggling the rail, which spends the double click it turns out to be the first half of
    ///     too: the toggle moved what was under the pointer, so the second click lands on whatever took the mark's place
    ///     — on the scrolled compact rail, a row of the content (#443).
    /// </summary>
    public bool ToggledRail { get; init; }

    /// <summary>
    ///     Whether it was spent on a mark — the boost or favorite of a post's <b>Counts</b> — which spends the double
    ///     click it turns out to be the first half of too: a second click there would take off the mark the first put on,
    ///     or put on again the one it took off, so a double click marks once (#593). A double click on any other run that
    ///     stands for a key is that click twice, as two slower clicks would be.
    /// </summary>
    public bool Marked { get; init; }

    /// <summary>
    ///     The reference it walked to, which the double click it turns out to be the first half of opens (#479): the
    ///     brackets that click drew may have moved the row a column on or wrapped a long address anew, so what is under
    ///     the pointer by the second click is not asked again.
    /// </summary>
    public Reference? WalkedTo { get; init; }

    /// <summary>
    ///     Where it was and until when a second press there would be the pair Terminal.Gui makes a double click of, where
    ///     it put another screen in front — the compose screen the <c>↩</c> of a post's <b>Counts</b> opens (#594), the
    ///     account a face of a notification group opens, the actors its names line does (#456). The rest of the pair is
    ///     spent before any view is asked: the screen in front was not what the reader aimed the second click at, and it
    ///     would land on whatever of the new one is drawn there, a choice on To or a place in the body.
    /// </summary>
    public SpentPair? PutAScreenInFront { get; init; }
}

/// <summary>
///     A click that put another screen in front (<see cref="LastClick.PutAScreenInFront" />): where it was, until when a
///     press there is the second of its pair, and whether that press has come and been spent.
/// </summary>
internal sealed record SpentPair(Point At, DateTimeOffset Until, bool Pressed = false);
