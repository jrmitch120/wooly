using Wooly.Core.Paging;
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

    private Func<string, PostThread>? _around;

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

    /// <summary>The access token every call was made with, in order — where a test proves who it was made as.</summary>
    public List<string> Tokens { get; } = [];

    /// <summary>Every mark it was asked to put on or take off, in order — where a test proves what a command asked for.</summary>
    public List<Marked> Marks { get; } = [];

    /// <summary>Every post it was asked to read, in order.</summary>
    public List<Shown> Reads { get; } = [];

    /// <summary>Every post it was asked for the thread around, in order.</summary>
    public List<Shown> ThreadsRead { get; } = [];

    /// <summary>Every vote it was asked to cast, in order — where a test proves what a front end chose.</summary>
    public List<Cast> Votes { get; } = [];

    /// <summary>
    ///     What a mark is answered with, where a test finishes it by hand — the question being what happens to one that
    ///     lands after the session has switched profile (#243).
    /// </summary>
    public Func<Task<Post>>? Marking { get; set; }

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

    /// <summary>
    ///     An instance holding a thread of its own around each post it is asked about, from <paramref name="around" />
    ///     given the post's id — where a test walks from one post into another and each screen must hold its own
    ///     thread (#482).
    /// </summary>
    public static FakePostEngagement Threading(Func<string, PostThread> around) =>
        new(APost.With(), refusal: null) { _around = around };

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
        Tokens.Add(profile.AccessToken);

        return Marking?.Invoke() ?? Answer();
    }

    public Task<Post> Show(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        Reads.Add(new Shown(profile.Name, postId));
        Tokens.Add(profile.AccessToken);

        return Answer();
    }

    public Task<PostThread> Thread(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        ThreadsRead.Add(new Shown(profile.Name, postId));
        Tokens.Add(profile.AccessToken);

        if (_around is { } around)
        {
            return Task.FromResult(around(postId));
        }

        return _refusal is null ? _thread() : Task.FromException<PostThread>(_refusal);
    }

    public Task<Post> Vote(
        ActiveProfile profile,
        Post post,
        IReadOnlyList<int> choices,
        CancellationToken cancellationToken)
    {
        Votes.Add(new Cast(profile.Name, post.Id, choices));
        Tokens.Add(profile.AccessToken);

        if ((VoteRefusal ?? _refusal) is { } refusal)
        {
            return Task.FromException<Post>(refusal);
        }

        // The post it was handed, unless a test said what the instance answers with — which is what the real adapter
        // does too: the poll comes back, and the post around it is the caller's own.
        return Task.FromResult(Voted ?? post);
    }

    /// <summary>
    ///     The actors it holds for each side of each post, keyed by post id and side, which a read of them answers from
    ///     — nobody, where a test put nobody there (#487).
    /// </summary>
    public Dictionary<(string PostId, ActorSide Side), IReadOnlyList<Wooly.Core.Accounts.Account>> ActorsOf { get; } = [];

    /// <summary>Every read of a post's actors it was asked for, in order.</summary>
    public List<ActorRead> ActorReads { get; } = [];

    /// <summary>Every read on of a post's actors it was asked for, in order — where a test proves where it went on from.</summary>
    public List<ActorReadOn> ActorReadsOn { get; } = [];

    public Task<Fetch<Wooly.Core.Accounts.Account>> ReadActors(
        ActiveProfile profile,
        string postId,
        ActorSide side,
        int limit,
        CancellationToken cancellationToken)
    {
        ActorReads.Add(new ActorRead(profile.Name, postId, side, limit));
        Tokens.Add(profile.AccessToken);

        return _refusal is null
            ? Task.FromResult(Actors(postId, side, from: null, limit))
            : Task.FromException<Fetch<Wooly.Core.Accounts.Account>>(_refusal);
    }

    public Task<Fetch<Wooly.Core.Accounts.Account>> ReadActorsOn(
        ActiveProfile profile,
        string postId,
        ActorSide side,
        Onward from,
        int limit,
        CancellationToken cancellationToken)
    {
        ActorReadsOn.Add(new ActorReadOn(profile.Name, postId, side, from, limit));
        Tokens.Add(profile.AccessToken);

        return _refusal is null
            ? Task.FromResult(Actors(postId, side, from, limit))
            : Task.FromException<Fetch<Wooly.Core.Accounts.Account>>(_refusal);
    }

    private Fetch<Wooly.Core.Accounts.Account> Actors(string postId, ActorSide side, Onward? from, int limit) =>
        OnwardRun.Of(ActorsOf.GetValueOrDefault((postId, side)) ?? [], actor => actor.Id, from, limit);

    private Task<Post> Answer() =>
        _refusal is null ? Task.FromResult(_answer) : Task.FromException<Post>(_refusal);

    internal sealed record Marked(string Profile, string PostId, PostMark Mark, bool Wanted);

    internal sealed record Shown(string Profile, string PostId);

    /// <summary>One read of a post's actors: which profile, which post, which side, and how many were wanted.</summary>
    internal sealed record ActorRead(string Profile, string PostId, ActorSide Side, int Limit);

    /// <summary>One read on of a post's actors: which profile, which post, which side, from where, and how many more.</summary>
    internal sealed record ActorReadOn(string Profile, string PostId, ActorSide Side, Onward From, int Limit);

    internal sealed record Cast(string Profile, string PostId, IReadOnlyList<int> Choices);
}
