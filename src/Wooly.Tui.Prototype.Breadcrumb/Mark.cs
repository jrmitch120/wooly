namespace Wooly.Tui.Prototype.Breadcrumb;

/// <summary>
///     How the fetch mark is spelled from one frame to the next (#213): the dots arrive one at a time and the letters
///     do not move, which is the dev's ask. Everything here is about the mark alone — the trail beside it is #168's.
/// </summary>
public static class Mark
{
    /// <summary>The word, which never changes and never moves.</summary>
    public const string Word = "fetching";

    /// <summary>
    ///     The columns the mark is laid out at: the word and the three dots it can reach. Today's <c>fetching…</c> is
    ///     nine on a single ellipsis, so three separate periods cost two columns more.
    /// </summary>
    public const int Widest = 11;

    /// <summary>What ships today, and what a terminal being read plainly might keep.</summary>
    public const string Still = "fetching…";

    /// <summary>One dot, two, three, and start over — the mark is never the bare word.</summary>
    public static readonly IReadOnlyList<string> Arriving = [".", "..", "..."];

    /// <summary>The same with a beat of nothing at the top, so starting over is a rest rather than a jump back.</summary>
    public static readonly IReadOnlyList<string> Breathing = ["", ".", "..", "..."];

    /// <summary>The rates worth looking at, and what each one is an argument for.</summary>
    public static readonly IReadOnlyList<Beat> Beats =
    [
        new("250ms", TimeSpan.FromMilliseconds(250), "four a second — the ticket's \"machine in trouble\""),
        new("350ms", TimeSpan.FromMilliseconds(350), "a cycle just over a second"),
        new("400ms", TimeSpan.FromMilliseconds(400), "two and a half a second, a 1.2s cycle"),
        new("500ms", TimeSpan.FromMilliseconds(500), "half the countdown's step"),
        new("1000ms", TimeSpan.FromMilliseconds(1000), "the countdown's own step — the ticket's \"stall\""),
    ];

    /// <summary>Which beat of the cycle <paramref name="since" /> has got to, counting from the fetch's start.</summary>
    public static int Phase(TimeSpan since, TimeSpan step) => (int)(since.TotalMilliseconds / step.TotalMilliseconds);

    /// <summary>
    ///     The mark as this frame spells it, or nothing where the fetch has not been in flight long enough to have
    ///     earned the row.
    /// </summary>
    /// <param name="since">How long this fetch has been in flight.</param>
    /// <param name="beat">How long one dot is held.</param>
    /// <param name="cycle">The spellings it walks through.</param>
    /// <param name="padded">
    ///     Whether the mark is laid out at <see cref="Widest" />. Unpadded is the jiggle: a mark eight columns on one
    ///     tick and eleven on the next re-elides the trail under it on every tick.
    /// </param>
    /// <param name="delayed">
    ///     Whether the mark waits for the first tick before it appears at all, so that a fetch landing in 80ms never
    ///     flashes a dot.
    /// </param>
    public static string Spelled(
        TimeSpan since,
        TimeSpan beat,
        IReadOnlyList<string> cycle,
        bool padded = true,
        bool delayed = true)
    {
        var phase = Phase(since, beat);

        if (delayed && phase == 0)
        {
            return string.Empty;
        }

        var text = Word + cycle[(delayed ? phase - 1 : phase) % cycle.Count];

        return padded ? text.PadRight(Widest) : text;
    }
}

/// <summary>A rate the dots could arrive at.</summary>
/// <param name="Name">What to call it while watching it.</param>
/// <param name="Step">How long one dot is held.</param>
/// <param name="Why">The argument for it beyond taste.</param>
public sealed record Beat(string Name, TimeSpan Step, string Why);
