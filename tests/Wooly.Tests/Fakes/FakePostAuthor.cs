using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     Writing posts without an instance to write to. ADR-0005's primary seam for anything above the API layer: a command
///     test says what came back and then asks what was composed, and never fakes HTTP to do it.
/// </summary>
internal sealed class FakePostAuthor : IPostAuthor
{
    private readonly Post _answer;
    private readonly Exception? _refusal;

    private FakePostAuthor(Post answer, Exception? refusal = null)
    {
        _answer = answer;
        _refusal = refusal;
    }

    /// <summary>Every draft it was asked to publish, in order — where a test proves what a command composed.</summary>
    public List<Composed> Published { get; } = [];

    /// <summary>Every edit it was asked to make, in order.</summary>
    public List<Changed> Edits { get; } = [];

    /// <summary>Every post it was asked to take down, in order.</summary>
    public List<Removed> Deletions { get; } = [];

    /// <summary>An instance that takes whatever it is given and answers with <paramref name="answer" />.</summary>
    public static FakePostAuthor Answering(Post? answer = null) => new(answer ?? APost.With());

    /// <summary>An instance that refuses everything with <paramref name="refusal" />, having recorded the attempt.</summary>
    public static FakePostAuthor Refusing(Exception refusal) => new(APost.With(), refusal);

    public Task<Post> Publish(ActiveProfile profile, PostDraft draft, CancellationToken cancellationToken)
    {
        Published.Add(new Composed(profile.Name, draft));

        return Answer();
    }

    public Task<Post> Edit(ActiveProfile profile, string postId, PostEdit edit, CancellationToken cancellationToken)
    {
        Edits.Add(new Changed(profile.Name, postId, edit));

        return Answer();
    }

    public Task Delete(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        Deletions.Add(new Removed(profile.Name, postId));

        return _refusal is null ? Task.CompletedTask : Task.FromException(_refusal);
    }

    private Task<Post> Answer() =>
        _refusal is null ? Task.FromResult(_answer) : Task.FromException<Post>(_refusal);

    internal sealed record Composed(string Profile, PostDraft Draft);

    internal sealed record Changed(string Profile, string PostId, PostEdit Edit);

    internal sealed record Removed(string Profile, string PostId);
}
