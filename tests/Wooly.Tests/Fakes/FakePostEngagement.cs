using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Marking and reading posts without an instance to mark them on. ADR-0005's primary seam for anything above the
///     API layer: a command test says what came back and then asks what was marked, and never fakes HTTP to do it.
/// </summary>
internal sealed class FakePostEngagement : IPostEngagement
{
    private readonly Post _answer;
    private readonly Exception? _refusal;

    private Func<Task<PostThread>> _thread;

    private FakePostEngagement(Post answer, Exception? refusal = null, PostThread? thread = null)
    {
        _answer = answer;
        _refusal = refusal;
        _thread = () => Task.FromResult(thread ?? PostThread.Alone);
    }

    private FakePostEngagement(Post answer, Func<Task<PostThread>> thread)
    {
        _answer = answer;
        _thread = thread;
    }

    /// <summary>Every mark it was asked to put on or take off, in order — where a test proves what a command asked for.</summary>
    public List<Marked> Marks { get; } = [];

    /// <summary>Every post it was asked to read, in order.</summary>
    public List<Shown> Reads { get; } = [];

    /// <summary>Every post it was asked for the thread around, in order.</summary>
    public List<Shown> ThreadsRead { get; } = [];

    /// <summary>Every vote it was asked to cast, in order — where a test proves what a front end chose.</summary>
    public List<Cast> Votes { get; } = [];

    /// <summary>The post a vote is answered with, where a test is about what the poll came back saying.</summary>
    public Post? Voted { get; set; }

    /// <summary>
    ///     What a vote is turned down with, where a test is about an instance refusing one — the second vote it will
    ///     not take. Its own knob rather than <see cref="Refusing" />, because reaching a vote at all means reading the
    ///     post that carries the poll first, and an instance that refused that read would be a different test.
    /// </summary>
    public Exception? VoteRefusal { get; set; }

    /// <summary>An instance that takes whatever it is asked and answers with <paramref name="answer" />.</summary>
    public static FakePostEngagement Answering(Post? answer = null) => new(answer ?? APost.With());

    /// <summary>An instance holding <paramref name="replies" /> in answer to whatever post it is asked about.</summary>
    public static FakePostEngagement Answered(Post answer, params Post[] replies) =>
        new(answer, refusal: null, new PostThread([], replies));

    /// <summary>
    ///     An instance holding a whole thread around whatever post it is asked about: what the post answers as well as
    ///     what answered it (#86).
    /// </summary>
    public static FakePostEngagement Threaded(Post answer, IReadOnlyList<Post> ancestors, params Post[] replies) =>
        new(answer, refusal: null, new PostThread(ancestors, replies));

    /// <summary>An instance that refuses everything with <paramref name="refusal" />, having recorded the attempt.</summary>
    public static FakePostEngagement Refusing(Exception refusal) => new(APost.With(), refusal);

    /// <summary>
    ///     An instance whose answers a test finishes by hand — where the question is what happens to a thread that
    ///     lands after the reader has walked out of the screen that asked for it (#84).
    /// </summary>
    public static FakePostEngagement Awaiting(Func<Task<PostThread>> thread) => new(APost.With(), thread);

    /// <summary>
    ///     What the post is answered with from here on: what was said while the reader was reading it, which is what a
    ///     refresh is asked to notice (#84).
    /// </summary>
    public void NowAnswered(params Post[] replies) =>
        _thread = () => Task.FromResult(new PostThread([], replies));

    public Task<Post> Mark(
        ActiveProfile profile,
        string postId,
        PostMark mark,
        bool wanted,
        CancellationToken cancellationToken)
    {
        Marks.Add(new Marked(profile.Name, postId, mark, wanted));

        return Answer();
    }

    public Task<Post> Show(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        Reads.Add(new Shown(profile.Name, postId));

        return Answer();
    }

    public Task<PostThread> Thread(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        ThreadsRead.Add(new Shown(profile.Name, postId));

        return _refusal is null ? _thread() : Task.FromException<PostThread>(_refusal);
    }

    public Task<Post> Vote(
        ActiveProfile profile,
        Post post,
        IReadOnlyList<int> choices,
        CancellationToken cancellationToken)
    {
        Votes.Add(new Cast(profile.Name, post.Id, choices));

        if ((VoteRefusal ?? _refusal) is { } refusal)
        {
            return Task.FromException<Post>(refusal);
        }

        // The post it was handed, unless a test said what the instance answers with — which is what the real adapter
        // does too: the poll comes back, and the post around it is the caller's own.
        return Task.FromResult(Voted ?? post);
    }

    private Task<Post> Answer() =>
        _refusal is null ? Task.FromResult(_answer) : Task.FromException<Post>(_refusal);

    internal sealed record Marked(string Profile, string PostId, PostMark Mark, bool Wanted);

    internal sealed record Shown(string Profile, string PostId);

    internal sealed record Cast(string Profile, string PostId, IReadOnlyList<int> Choices);
}
