namespace Wooly.Tui.Shell;

/// <summary>
///     The **Shell width** (<c>GLOSSARY.md</c>): how much of the terminal's width the shell takes, as a whole-number
///     percentage — all of it, or less in steps of ten down to half — and so which columns it is drawn in on a terminal of
///     a given width (#413, #414).
/// </summary>
/// <remarks>
///     A share rather than a count of columns, so that it holds when the terminal is resized; the columns are worked out
///     afresh from the terminal's width on every layout (<see cref="Columns" />, <see cref="Left" />), which is all
///     re-centring after a resize takes. Never under the <see cref="Floor" /> the shell is laid out for, whatever the
///     share: on a terminal that leaves no room for less, the shell is all of it.
/// </remarks>
public sealed record ShellWidth
{
    /// <summary>The fewest columns the shell is ever drawn in: the 80 every screen is laid out to read well at.</summary>
    public const int Floor = 80;

    /// <summary>The narrowest share there is: half the terminal.</summary>
    public const int Narrowest = 50;

    /// <summary>The widest: all of it, which is what a reader who has never changed it has.</summary>
    public const int Widest = 100;

    /// <summary>How far one press of <c>-</c> or <c>+</c> moves it, onto a multiple of which it always lands.</summary>
    private const int Step = 10;

    private ShellWidth(int percent) => Percent = percent;

    /// <summary>The whole terminal.</summary>
    public static ShellWidth Full { get; } = new(Widest);

    /// <summary>The share of the terminal's width, from <see cref="Narrowest" /> to <see cref="Widest" />.</summary>
    public int Percent { get; }

    /// <summary>
    ///     <paramref name="percent" /> of the terminal, brought into range rather than refused — so an in-between value
    ///     such as 73 stands as written, and one out of range is the nearest end of it.
    /// </summary>
    public static ShellWidth Of(int percent) => new(Math.Clamp(percent, Narrowest, Widest));

    /// <summary>
    ///     One step narrower, on a terminal <paramref name="terminal" /> columns wide: the largest multiple of ten below
    ///     this that is at least <see cref="Narrowest" /> — or <see langword="null" /> where there is none, or where it
    ///     would take the shell under <see cref="Floor" /> columns on this terminal.
    /// </summary>
    public ShellWidth? Narrower(int terminal)
    {
        var step = (Percent - 1) / Step * Step;

        return step >= Narrowest && Share(terminal, step) >= Floor ? new ShellWidth(step) : null;
    }

    /// <summary>
    ///     One step wider: the smallest multiple of ten above this that is at most <see cref="Widest" />, or
    ///     <see langword="null" /> at the whole terminal already. Never refused for the terminal's sake, since nothing
    ///     wider can break the floor — on a terminal too narrow for it to show, it still moves.
    /// </summary>
    public ShellWidth? Wider()
    {
        var step = (Percent / Step + 1) * Step;

        return step <= Widest ? new ShellWidth(step) : null;
    }

    /// <summary>
    ///     How many columns the shell takes on a terminal <paramref name="terminal" /> columns wide: its share, rounded
    ///     down, but never under <see cref="Floor" /> nor over the terminal.
    /// </summary>
    public int Columns(int terminal) => Math.Min(terminal, Math.Max(Floor, Share(terminal, Percent)));

    /// <summary>
    ///     The first of those columns, centring the shell — the spare column of an odd remainder falling on the right, so
    ///     that the left edge moves only when the width does.
    /// </summary>
    public int Left(int terminal) => (terminal - Columns(terminal)) / 2;

    private static int Share(int terminal, int percent) => terminal * percent / 100;
}
