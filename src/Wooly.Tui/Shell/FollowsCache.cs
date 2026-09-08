using Wooly.Core.Accounts;
using Wooly.Core.Relationships;

namespace Wooly.Tui.Shell;

/// <summary>
///     What a follow list last held, for a short while — keyed by whose list it is and which side of it, which is the
///     pair that names one (#180).
/// </summary>
/// <remarks>
///     The shell's first cache over a screen the stack was drilled into rather than a rail destination, and so a
///     cache of its own: <see cref="DestinationCache" /> is keyed by <see cref="DestinationKind" />, one entry per
///     destination, and a follow list is one of many under an account.
///     <para>
///         Age only, and the same minute a destination is held for, for the reason that one gives: the question is
///         "is this still the list I just left", and that has the same answer wherever it is asked. It pays only on a
///         re-open after popping — walking back out is already free, the stack handing back the very screen with its
///         page intact (#133) — and holds nothing that a list this client changed could make stale, a follow put on
///         from an account screen being a change to a standing rather than to who is on somebody's list.
///     </para>
/// </remarks>
/// <param name="clock">What an entry's age is measured against.</param>
/// <param name="freshFor">How long what a list held stays worth drawing without asking again.</param>
public sealed class FollowsCache(TimeProvider clock, TimeSpan freshFor)
{
    private readonly Dictionary<(string Account, FollowSide Side), Held> _held = [];

    /// <summary>
    ///     Who was on <paramref name="side" /> of <paramref name="accountId" />'s follows, if that was read recently
    ///     enough to draw without asking again.
    /// </summary>
    public IReadOnlyList<Account>? Fresh(string accountId, FollowSide side) =>
        _held.TryGetValue((accountId, side), out var held) && clock.GetUtcNow() - held.At < freshFor
            ? held.People
            : null;

    /// <summary>
    ///     Takes down who is on that side now — only ever the whole of a list, since a page of a browsed one is not
    ///     something to hand back as though it were what is there.
    /// </summary>
    public void Keep(string accountId, FollowSide side, IReadOnlyList<Account> people) =>
        _held[(accountId, side)] = new Held(clock.GetUtcNow(), people);

    /// <summary>Forgets it, which is what <c>g</c> does before it reads the list again.</summary>
    public void Forget(string accountId, FollowSide side) => _held.Remove((accountId, side));

    private sealed record Held(DateTimeOffset At, IReadOnlyList<Account> People);
}
