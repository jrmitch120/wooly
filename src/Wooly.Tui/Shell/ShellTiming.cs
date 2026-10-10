namespace Wooly.Tui.Shell;

/// <summary>
///     The eight lengths of time the shell's behaviour depends on, in one place so that a test can shorten them and
///     a reader can find out what they are without reading for them — and, beside them, the one size it depends on
///     (<see cref="FollowsHeldUnder" />), which is not a length of time and is here to be found with them.
/// </summary>
/// <param name="Settle">
///     How long the tabbing has to stop for before the rail's selection follows its cursor. 250ms: long enough that a
///     deliberate double-tap lands as one move, short enough that a single tab does not read as a pause (ADR-0014). Also
///     how long typing an <c>@</c>-word has to stop for before the instance is asked to search the profile's follows for
///     it (#322), for the same reason: a run of letters is one ask rather than one per letter.
/// </param>
/// <param name="CacheFor">
///     How long what a destination held stays worth drawing without asking the instance again. A minute: long enough
///     that walking out along the rail and back is free, short enough that a timeline left and returned to a minute
///     later is fetched rather than remembered.
/// </param>
/// <param name="CountdownStep">
///     How often the rate-limit countdown is redrawn while it waits. A second, because that is the unit it counts in.
/// </param>
/// <param name="MarkStep">
///     How long one frame of the breadcrumb's spinner is held, and — since the mark waits for its first tick — how
///     long a fetch runs before it is announced at all. 400ms: the slowest rate a glance still catches moving, and
///     deliberately not the countdown's second, which would say the mark counts something (#213).
/// </param>
/// <param name="PollEvery">
///     How often the Inbox badges are read again for the profile acted as (#398). A minute: often enough that nothing
///     waits long to be noticed, seldom enough that three reads a minute are nothing against a budget of 300 every five.
/// </param>
/// <param name="ReconnectAfter">
///     How long after the user stream drops, or will not connect, it is tried again the first time (#401) — twice as
///     long each time after that, up to <paramref name="ReconnectAtMost" />, and back to this once it has come up. Two
///     seconds: a blip is over by then, and an instance that is down is not asked again at once.
/// </param>
/// <param name="ReconnectAtMost">
///     The longest the user stream is left before it is tried again (#401). Two minutes: the badges are polled every
///     minute meanwhile, so nothing waits on it, and an instance that will not stream is asked thirty times an hour.
/// </param>
/// <param name="GatherFor">
///     How long what the user stream says is gathered before the counts it touches are read again, as one re-read
///     (#401). A direct message comes over the stream twice — a mention, then a conversation — and raises two badges,
///     and one arrival is one bell. Two seconds: long enough for the instance to have sent both, short enough that a
///     badge still rises within moments.
/// </param>
public sealed record ShellTiming(
    TimeSpan Settle,
    TimeSpan CacheFor,
    TimeSpan CountdownStep,
    TimeSpan MarkStep,
    TimeSpan PollEvery,
    TimeSpan ReconnectAfter,
    TimeSpan ReconnectAtMost,
    TimeSpan GatherFor)
{
    /// <summary>
    ///     How many people a follow list may have on it and still be held whole rather than browsed a page at a time
    ///     (#180). Two thousand: at 80 to a page that is 25 requests against a budget of 300 every five minutes, and
    ///     the list above it — <c>@Mastodon@mastodon.social</c>'s 877,000 followers — would be some 11,000.
    /// </summary>
    /// <remarks>
    ///     A count rather than a length of time, and here anyway: it is the ninth thing the shell's behaviour turns
    ///     on that a reader would otherwise go looking for, and a constant found beside the eight it belongs with is
    ///     worth more than one filed under the right type. Not settable the way the eight are — a test shortening a
    ///     wait is asking for the same behaviour sooner, where a test moving this would be asking for another mode.
    /// </remarks>
    public const int FollowsHeldUnder = 2_000;

    /// <summary>What the shell runs at in front of a person.</summary>
    public static ShellTiming Default { get; } = new(
        Settle: TimeSpan.FromMilliseconds(250),
        CacheFor: TimeSpan.FromMinutes(1),
        CountdownStep: TimeSpan.FromSeconds(1),
        MarkStep: TimeSpan.FromMilliseconds(400),
        PollEvery: TimeSpan.FromSeconds(60),
        ReconnectAfter: TimeSpan.FromSeconds(2),
        ReconnectAtMost: TimeSpan.FromMinutes(2),
        GatherFor: TimeSpan.FromSeconds(2));
}
