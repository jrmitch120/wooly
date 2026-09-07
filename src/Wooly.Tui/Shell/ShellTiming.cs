namespace Wooly.Tui.Shell;

/// <summary>
///     The three lengths of time the shell's behaviour depends on, in one place so that a test can shorten them and
///     a reader can find out what they are without reading for them — and, beside them, the one size it depends on
///     (<see cref="FollowsHeldUnder" />), which is not a length of time and is here to be found with them.
/// </summary>
/// <param name="Settle">
///     How long the tabbing has to stop for before the rail's selection follows its cursor. 250ms: long enough that a
///     deliberate double-tap lands as one move, short enough that a single tab does not read as a pause (ADR-0014).
/// </param>
/// <param name="CacheFor">
///     How long what a destination held stays worth drawing without asking the instance again. A minute: long enough
///     that walking out along the rail and back is free, short enough that a timeline left and returned to a minute
///     later is fetched rather than remembered.
/// </param>
/// <param name="CountdownStep">
///     How often the rate-limit countdown is redrawn while it waits. A second, because that is the unit it counts in.
/// </param>
public sealed record ShellTiming(TimeSpan Settle, TimeSpan CacheFor, TimeSpan CountdownStep)
{
    /// <summary>
    ///     How many people a follow list may have on it and still be held whole rather than browsed a page at a time
    ///     (#180). Two thousand: at 80 to a page that is 25 requests against a budget of 300 every five minutes, and
    ///     the list above it — <c>@Mastodon@mastodon.social</c>'s 877,000 followers — would be some 11,000.
    /// </summary>
    /// <remarks>
    ///     A count rather than a length of time, and here anyway: it is the fourth thing the shell's behaviour turns
    ///     on that a reader would otherwise go looking for, and a constant found beside the three it belongs with is
    ///     worth more than one filed under the right type. Not settable the way the three are — a test shortening a
    ///     wait is asking for the same behaviour sooner, where a test moving this would be asking for another mode.
    /// </remarks>
    public const int FollowsHeldUnder = 2_000;

    /// <summary>What the shell runs at in front of a person.</summary>
    public static ShellTiming Default { get; } = new(
        Settle: TimeSpan.FromMilliseconds(250),
        CacheFor: TimeSpan.FromMinutes(1),
        CountdownStep: TimeSpan.FromSeconds(1));
}
