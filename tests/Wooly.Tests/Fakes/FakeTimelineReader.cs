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
    private Func<Timeline, Task<Fetch<Post>>> _answer;

    private FakeTimelineReader(Func<Timeline, Task<Fetch<Post>>> answer) => _answer = answer;

    public FakeTimelineReader(Fetch<Post> fetch) : this(_ => Task.FromResult(fetch))
    {
    }

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
    ///     What every timeline holds from here on: what the instance did while the reader was reading it, which is
    ///     what a refresh is asked to notice (#84).
    /// </summary>
    public void NowHolding(params Post[] posts)
    {
        var fetch = Fetch<Post>.Complete(posts);

        _answer = _ => Task.FromResult(fetch);
    }

    public Task<Fetch<Post>> Read(
        ActiveProfile profile,
        Timeline timeline,
        int limit,
        CancellationToken cancellationToken)
    {
        Reads.Add(new Call(profile.Name, timeline, limit));

        return _answer(timeline);
    }

    /// <summary>One call: which profile it was made as, which timeline it asked for, and how many posts it wanted.</summary>
    internal sealed record Call(string Profile, Timeline Timeline, int Limit);
}
