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
    private readonly List<Action> _held = [];

    private Func<Timeline, Task<Fetch<Post>>> _answer;

    private Func<Onward?, int, Fetch<Post>>? _reading;

    private bool _holding;

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
    public static FakeTimelineReader ReadingOn(IReadOnlyList<Post> posts)
    {
        var reader = Holding();

        reader._reading = (from, limit) => Run(posts, from, limit);

        return reader;
    }

    /// <summary>Every read on it was asked for, in order — where a test proves where a read went on from.</summary>
    public List<OnwardCall> ReadsOn { get; } = [];

    /// <summary>
    ///     Holds every read on from here on until <see cref="LetReadsOnGo" /> — where the question is what happens
    ///     while one is in flight, or to one that lands after the reader has moved on.
    /// </summary>
    public void HoldReadsOn() => _holding = true;

    /// <summary>Answers every read on held so far.</summary>
    public void LetReadsOnGo()
    {
        var held = _held.ToList();

        _held.Clear();
        _holding = false;
        held.ForEach(release => release());
    }

    public Task<Fetch<Post>> Read(
        ActiveProfile profile,
        Timeline timeline,
        int limit,
        CancellationToken cancellationToken)
    {
        Reads.Add(new Call(profile.Name, timeline, limit));
        Tokens.Add(profile.AccessToken);

        return _reading is { } reading ? Task.FromResult(reading(null, limit)) : _answer(timeline);
    }

    public Task<Fetch<Post>> ReadOn(
        ActiveProfile profile,
        Timeline timeline,
        Onward from,
        int limit,
        CancellationToken cancellationToken)
    {
        ReadsOn.Add(new OnwardCall(timeline, from, limit));
        Tokens.Add(profile.AccessToken);

        // A reader nobody said reads on has nothing behind what it handed out, which is a list read whole.
        var answer = _reading?.Invoke(from, limit) ?? Fetch<Post>.Complete([]);

        if (!_holding)
        {
            return Task.FromResult(answer);
        }

        var held = new TaskCompletionSource<Fetch<Post>>();

        _held.Add(() => held.SetResult(answer));

        return held.Task;
    }

    /// <summary>
    ///     <paramref name="limit" /> of <paramref name="posts" /> from just past the post <paramref name="from" />
    ///     names, and where to read on from after them unless that was the last of them.
    /// </summary>
    private static Fetch<Post> Run(IReadOnlyList<Post> posts, Onward? from, int limit)
    {
        var start = from?.MaxId is { } after ? posts.ToList().FindIndex(post => post.Id == after) + 1 : 0;
        var run = posts.Skip(start).Take(limit).ToList();

        return start + run.Count < posts.Count
            ? Fetch<Post>.Complete(run, new Onward(run[^1].Id))
            : Fetch<Post>.Complete(run);
    }

    /// <summary>One call: which profile it was made as, which timeline it asked for, and how many posts it wanted.</summary>
    internal sealed record Call(string Profile, Timeline Timeline, int Limit);

    /// <summary>One read on: which timeline, from where, and how many more posts it wanted.</summary>
    internal sealed record OnwardCall(Timeline Timeline, Onward From, int Limit);
}
