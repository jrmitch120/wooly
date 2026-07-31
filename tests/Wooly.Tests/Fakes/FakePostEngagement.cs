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
    private readonly IReadOnlyList<Post> _replies;

    private FakePostEngagement(Post answer, Exception? refusal = null, IReadOnlyList<Post>? replies = null)
    {
        _answer = answer;
        _refusal = refusal;
        _replies = replies ?? [];
    }

    /// <summary>Every mark it was asked to put on or take off, in order — where a test proves what a command asked for.</summary>
    public List<Marked> Marks { get; } = [];

    /// <summary>Every post it was asked to read, in order.</summary>
    public List<Shown> Reads { get; } = [];

    /// <summary>Every post it was asked for the answers to, in order.</summary>
    public List<Shown> RepliesRead { get; } = [];

    /// <summary>An instance that takes whatever it is asked and answers with <paramref name="answer" />.</summary>
    public static FakePostEngagement Answering(Post? answer = null) => new(answer ?? APost.With());

    /// <summary>An instance holding <paramref name="replies" /> in answer to whatever post it is asked about.</summary>
    public static FakePostEngagement Answered(Post answer, params Post[] replies) =>
        new(answer, refusal: null, replies);

    /// <summary>An instance that refuses everything with <paramref name="refusal" />, having recorded the attempt.</summary>
    public static FakePostEngagement Refusing(Exception refusal) => new(APost.With(), refusal);

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

    public Task<IReadOnlyList<Post>> Replies(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        RepliesRead.Add(new Shown(profile.Name, postId));

        return _refusal is null
            ? Task.FromResult(_replies)
            : Task.FromException<IReadOnlyList<Post>>(_refusal);
    }

    private Task<Post> Answer() =>
        _refusal is null ? Task.FromResult(_answer) : Task.FromException<Post>(_refusal);

    internal sealed record Marked(string Profile, string PostId, PostMark Mark, bool Wanted);

    internal sealed record Shown(string Profile, string PostId);
}
