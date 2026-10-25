using Wooly.Core.Errors;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An instance's timelines without the instance. ADR-0005's primary seam for anything above the API layer: a
///     command test says what came back and then asks what was read, and never fakes HTTP to do it.
/// </summary>
internal sealed class FakeTimelineReader : ITimelineReader
{
    private readonly HeldReadsOn _held = new();

    private Func<Timeline, Task<Fetch<Post>>> _answer;

    private Func<Timeline, Onward?, int, Fetch<Post>>? _reading;

    private Func<Timeline, Onward, int, Task<Fetch<Post>>>? _nextReadOn;

    private Exception? _refusing;

    private RateLimitedException? _nextReadStoppedBy;

    private FakeTimelineReader(Func<Timeline, Task<Fetch<Post>>> answer) => _answer = answer;

    public FakeTimelineReader(Fetch<Post> fetch) : this(_ => Task.FromResult(fetch))
    {
    }

    /// <summary>The access token every call was made with, in order — where a test proves who it was made as.</summary>
    public List<string> Tokens { get; } = [];

    /// <summary>Every read it was asked for, in order — where a test proves which timeline a command went for.</summary>
    public List<Call> Reads { get; } = [];

    /// <summary>A timeline holding <paramref name="posts" />, read to the end of whatever was asked for.</summary>
    public static FakeTimelineReader Holding(params Post[] posts) => new(Fetch<Post>.Complete(posts));

    /// <summary>
    ///     An instance that answers each timeline differently — where a test proves that the destination it drew is
    ///     the destination it asked for.
    /// </summary>
    public static FakeTimelineReader Answering(Func<Timeline, Fetch<Post>> answer) =>
        new(timeline => Task.FromResult(answer(timeline)));

    /// <summary>
    ///     An instance whose answer a test finishes by hand — where the question is what happens to one that lands
    ///     after the reader has moved on.
    /// </summary>
    public static FakeTimelineReader Awaiting(Func<Timeline, Task<Fetch<Post>>> answer) => new(answer);

    /// <summary>An instance whose rate limit stopped the read with <paramref name="posts" /> already in hand.</summary>
    public static FakeTimelineReader RateLimitedAfter(params Post[] posts) =>
        new(Fetch<Post>.StoppedShort(
            posts,
            new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero))));

    /// <summary>
    ///     An instance that refuses every read with <paramref name="refusal" />, having recorded the attempt — where the
    ///     question is what a command does with an address naming nobody.
    /// </summary>
    public static FakeTimelineReader Refusing(Exception refusal) =>
        new(_ => Task.FromException<Fetch<Post>>(refusal));

    /// <summary>
    ///     What every timeline holds from here on: what the instance did while the reader was reading it, which is
    ///     what a refresh is asked to notice (#84).
    /// </summary>
    public void NowHolding(params Post[] posts)
    {
        var fetch = Fetch<Post>.Complete(posts);

        _answer = _ => Task.FromResult(fetch);
    }

    /// <summary>
    ///     A timeline holding <paramref name="posts" />, newest first, which reads on (#460): every read hands out as
    ///     many as were asked for and where to read on from — the last post it handed out — until the posts run out,
    ///     where the timeline is whole.
    /// </summary>
    public static FakeTimelineReader ReadingOn(IReadOnlyList<Post> posts) => ReadingOn(_ => posts);

    /// <summary>
    ///     Every timeline holding what <paramref name="posts" /> says it holds, newest first, each read on the way
    ///     <see cref="ReadingOn(IReadOnlyList{Post})" /> reads one — where a test proves which timeline a read on went
    ///     to, an account's posts read on and their pinned run not (#464).
    /// </summary>
    public static FakeTimelineReader ReadingOn(Func<Timeline, IReadOnlyList<Post>> posts)
    {
        var reader = Holding();

        reader._reading = (timeline, from, limit) => OnwardRun.Of(posts(timeline), post => post.Id, from, limit);

        return reader;
    }

    /// <summary>Every read on it was asked for, in order — where a test proves where a read went on from.</summary>
    public List<TimelineReadOn> ReadsOn { get; } = [];

    /// <summary>
    ///     Holds every read on from here on until <see cref="LetReadsOnGo" /> — where the question is what happens
    ///     while one is in flight, or to one that lands after the reader has moved on.
    /// </summary>
    public void HoldReadsOn() => _held.Hold();

    /// <summary>Answers every read on held so far.</summary>
    public void LetReadsOnGo() => _held.LetGo();

    public Task<Fetch<Post>> Read(
        ActiveProfile profile,
        Timeline timeline,
        int limit,
        CancellationToken cancellationToken)
    {
        Reads.Add(new Call(profile.Name, timeline, limit));
        Tokens.Add(profile.AccessToken);

        if (_nextReadStoppedBy is { } stoppedBy)
        {
            _nextReadStoppedBy = null;

            return Task.FromResult(Fetch<Post>.StoppedShort([], stoppedBy));
        }

        return _reading is { } reading ? Task.FromResult(reading(timeline, null, limit)) : _answer(timeline);
    }

    /// <summary>
    ///     Every read on fails with <paramref name="refusal" /> from here on, having been recorded, until
    ///     <see cref="StopsRefusingReadsOn" /> — where the question is what a read on that did not succeed leaves
    ///     behind, and what tries it again (#462).
    /// </summary>
    public void RefusesReadsOn(Exception refusal) => _refusing = refusal;

    /// <summary>
    ///     The next read is stopped by <paramref name="limit" /> before anything arrives, as a rate limit on a list's
    ///     first page stops it — which reads on from the top of the list.
    /// </summary>
    public void RateLimitsNextRead(RateLimitedException limit) => _nextReadStoppedBy = limit;

    /// <summary>Reads on succeed again, as an instance that has come back does.</summary>
    public void StopsRefusingReadsOn() => _refusing = null;

    /// <summary>
    ///     The next read on hands out <paramref name="posts" /> of what it would have and is then stopped by
    ///     <paramref name="limit" />, keeping where it stopped — as a rate limit part way through a read does (#462).
    /// </summary>
    public void RateLimitsNextReadOnAfter(int posts, RateLimitedException limit) =>
        _nextReadOn = (timeline, from, _) =>
        {
            var arrived = _reading?.Invoke(timeline, from, posts).Items ?? [];

            return Task.FromResult(
                Fetch<Post>.StoppedShort(arrived, limit, arrived.Count > 0 ? new Onward(arrived[^1].Id) : from));
        };

    public Task<Fetch<Post>> ReadOn(
        ActiveProfile profile,
        Timeline timeline,
        Onward from,
        int limit,
        CancellationToken cancellationToken)
    {
        ReadsOn.Add(new TimelineReadOn(timeline, from, limit));
        Tokens.Add(profile.AccessToken);

        if (_refusing is { } refusal)
        {
            return Task.FromException<Fetch<Post>>(refusal);
        }

        if (_nextReadOn is { } next)
        {
            _nextReadOn = null;

            return next(timeline, from, limit);
        }

        // A reader nobody said reads on has nothing behind what it handed out, which is a list read whole.
        return _held.Answer(_reading?.Invoke(timeline, from, limit) ?? Fetch<Post>.Complete([]));
    }

    /// <summary>One call: which profile it was made as, which timeline it asked for, and how many posts it wanted.</summary>
    internal sealed record Call(string Profile, Timeline Timeline, int Limit);

    /// <summary>One read on: which timeline, from where, and how many more posts it wanted.</summary>
    internal sealed record TimelineReadOn(Timeline Timeline, Onward From, int Limit);
}
