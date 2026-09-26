namespace Wooly.Tui.Shell;

/// <summary>
///     What each subject that is <see cref="Subject.Cached" /> last held, for a short while. This is what makes walking
///     out along the rail and back one fetch per destination rather than one per arrival (ADR-0014) — a reader who
///     tabs past Local to Federated and back has not asked for Local twice — and what makes a follow list re-opened
///     after popping out of it free (#180).
/// </summary>
/// <remarks>
///     Deliberately short and deliberately dumb. It has no idea whether anything has changed on the instance, so the
///     age is the whole of its judgement, and the age is set where a timeline is still the timeline you just left
///     rather than where it is still current — those are different questions and only the first one is answerable
///     here. Anything older is fetched again.
///     <para>
///         One cache keyed by subject rather than one per kind of thing held, for the same reason
///         <c>docs/tui-shell.md</c> gives for one cache age: the question is "is this still what I just left", and that
///         has the same answer wherever you left from (#233).
///     </para>
/// </remarks>
/// <param name="clock">What an entry's age is measured against.</param>
/// <param name="freshFor">How long what a subject held stays worth drawing without asking again.</param>
public sealed class SubjectCache(TimeProvider clock, TimeSpan freshFor)
{
    private readonly Dictionary<Subject, Held> _held = [];

    /// <summary>How long what a subject held stays worth drawing.</summary>
    public TimeSpan FreshFor { get; } = freshFor;

    /// <summary>
    ///     Forgets what <paramref name="subject" /> held, for when this client is the thing that changed it — a post
    ///     published, deleted or marked makes the timeline it is on stale at once, whatever its age says, and so do a
    ///     notification dismissed and a follow request answered — and for <c>g</c>, which asks again.
    /// </summary>
    public void Forget(Subject subject) => _held.Remove(subject);

    /// <summary>
    ///     What <paramref name="subject" /> held, if it held anything recently enough to draw without asking again.
    /// </summary>
    internal Found? Fresh(Subject subject) =>
        _held.TryGetValue(subject, out var held) && clock.GetUtcNow() - held.At < FreshFor ? held.What : null;

    /// <summary>Takes down what <paramref name="subject" /> holds now.</summary>
    internal void Keep(Subject subject, Found what) => _held[subject] = new Held(clock.GetUtcNow(), what);

    private sealed record Held(DateTimeOffset At, Found What);
}
