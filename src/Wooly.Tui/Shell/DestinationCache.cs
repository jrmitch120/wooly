namespace Wooly.Tui.Shell;

/// <summary>
///     What each destination last held, for a short while. This is what makes walking out along the rail and back one
///     fetch per destination rather than one per arrival (ADR-0014) — a reader who tabs past Local to Federated and
///     back has not asked for Local twice.
/// </summary>
/// <remarks>
///     Deliberately short and deliberately dumb. It has no idea whether anything has changed on the instance, so the
///     age is the whole of its judgement, and the age is set where a timeline is still the timeline you just left
///     rather than where it is still current — those are different questions and only the first one is answerable
///     here. Anything older is fetched again.
///     <para>
///         One cache for every kind of thing a destination holds — posts, notifications, the accounts waiting to be
///         let in — rather than one per kind, for the same reason <c>docs/tui-shell.md</c> gives for one cache age:
///         the question is "is this still what I just left", and that has the same answer wherever you left from.
///     </para>
/// </remarks>
public sealed class DestinationCache(TimeProvider clock, TimeSpan freshFor)
{
    private readonly Dictionary<DestinationKind, Held> _held = [];

    /// <summary>How long what a destination held stays worth drawing.</summary>
    public TimeSpan FreshFor { get; } = freshFor;

    /// <summary>
    ///     What <paramref name="kind" /> held, if it held anything recently enough to draw without asking again.
    /// </summary>
    /// <typeparam name="T">
    ///     What that destination holds. A destination holds one kind of thing and always the same one, so asking for
    ///     the wrong one reads as a miss and costs a fetch, rather than throwing at somebody who is only reading.
    /// </typeparam>
    public IReadOnlyList<T>? Fresh<T>(DestinationKind kind) =>
        _held.TryGetValue(kind, out var held) && clock.GetUtcNow() - held.At < FreshFor
            ? held.What as IReadOnlyList<T>
            : null;

    /// <summary>Takes down what <paramref name="kind" /> holds now.</summary>
    public void Keep<T>(DestinationKind kind, IReadOnlyList<T> what) => _held[kind] = new Held(clock.GetUtcNow(), what);

    /// <summary>
    ///     Forgets what <paramref name="kind" /> held, for when this client is the thing that changed it — a post
    ///     published, deleted or marked makes the timeline it is on stale at once, whatever its age says, and so do a
    ///     notification dismissed and a follow request answered.
    /// </summary>
    public void Forget(DestinationKind kind) => _held.Remove(kind);

    private sealed record Held(DateTimeOffset At, object What);
}
