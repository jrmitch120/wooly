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

    /// <summary>The access token every call was made with, in order — where a test proves who it was made as.</summary>
    public List<string> Tokens { get; } = [];

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
        Tokens.Add(profile.AccessToken);

        return Answer();
    }

    /// <summary>
    ///     Every file it was asked to send up as a pending attachment, in order (#375) — each one held where it is until
    ///     the test moves it along: going up, processing, ready, or refused.
    /// </summary>
    public List<Sending> Attaching { get; } = [];

    /// <remarks>
    ///     Answers nothing until the test says, through the <see cref="Sending" /> it is recorded as — so a shell test
    ///     sees each state an attachment passes through, one at a time, with no network and no clock.
    /// </remarks>
    public Task<PendingAttachment> Attach(
        ActiveProfile profile,
        string path,
        IProgress<AttachmentProgress> progress,
        CancellationToken cancellationToken)
    {
        var sending = new Sending(profile.Name, path, $"m{Attaching.Count + 1}", progress, cancellationToken);

        Attaching.Add(sending);
        Tokens.Add(profile.AccessToken);

        return sending.Answer;
    }

    /// <summary>
    ///     Every description it was asked to put on a pending attachment, in order (#377) — each answered at once unless
    ///     <see cref="HoldingDescriptions" />, when it waits for the test to <see cref="Describing.Land" /> it.
    /// </summary>
    public List<Describing> Descriptions { get; } = [];

    /// <summary>Whether a description waits to be landed by the test rather than being taken at once.</summary>
    public bool HoldingDescriptions { get; set; }

    /// <summary>What every description is refused with, where the test wants them refused.</summary>
    public Exception? RefusingDescriptions { get; set; }

    public Task Describe(
        ActiveProfile profile,
        string attachmentId,
        string description,
        CancellationToken cancellationToken)
    {
        var describing = new Describing(profile.Name, attachmentId, description);

        Descriptions.Add(describing);
        Tokens.Add(profile.AccessToken);

        if (RefusingDescriptions is { } refusal)
        {
            describing.Refuse(refusal);
        }
        else if (!HoldingDescriptions)
        {
            describing.Land();
        }

        return describing.Answer;
    }

    /// <remarks>Recorded with every other publish, in <see cref="Published" />: what was attached is on the draft.</remarks>
    public Task<Post> PublishAttached(ActiveProfile profile, PostDraft draft, CancellationToken cancellationToken) =>
        Publish(profile, draft, cancellationToken);

    public Task<Post> Edit(ActiveProfile profile, string postId, PostEdit edit, CancellationToken cancellationToken)
    {
        Edits.Add(new Changed(profile.Name, postId, edit));
        Tokens.Add(profile.AccessToken);

        return Answer();
    }

    public Task Delete(ActiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        Deletions.Add(new Removed(profile.Name, postId));
        Tokens.Add(profile.AccessToken);

        return _refusal is null ? Task.CompletedTask : Task.FromException(_refusal);
    }

    private Task<Post> Answer() =>
        _refusal is null ? Task.FromResult(_answer) : Task.FromException<Post>(_refusal);

    internal sealed record Composed(string Profile, PostDraft Draft);

    internal sealed record Changed(string Profile, string PostId, PostEdit Edit);

    internal sealed record Removed(string Profile, string PostId);

    /// <summary>One description being put on a pending attachment, which the test lands or refuses.</summary>
    internal sealed record Describing(string Profile, string Id, string Description)
    {
        private readonly TaskCompletionSource _answer = new();

        public Task Answer => _answer.Task;

        /// <summary>The instance has taken it.</summary>
        public void Land() => _answer.TrySetResult();

        /// <summary>The instance would not take it.</summary>
        public void Refuse(Exception refusal) => _answer.TrySetException(refusal);
    }

    /// <summary>One file being sent up, which the test moves through the states an instance would.</summary>
    /// <param name="Profile">Who it was sent up as.</param>
    /// <param name="Path">The file.</param>
    /// <param name="Id">The id the instance gives it once it is ready: <c>m1</c>, <c>m2</c>, … in the order sent.</param>
    internal sealed class Sending(
        string profile,
        string path,
        string id,
        IProgress<AttachmentProgress> progress,
        CancellationToken cancellationToken)
    {
        private readonly TaskCompletionSource<PendingAttachment> _answer = Registered(cancellationToken);

        public string Profile { get; } = profile;

        public string Path { get; } = path;

        public string Id { get; } = id;

        /// <summary>Whether whoever sent it called it off, which a compose screen thrown away does.</summary>
        public bool CalledOff => cancellationToken.IsCancellationRequested;

        public Task<PendingAttachment> Answer => _answer.Task;

        /// <summary>It is <paramref name="done" /> of the way up.</summary>
        public void Progress(double done) => progress.Report(new AttachmentProgress.Sending(done));

        /// <summary>It is up, and the instance is processing it.</summary>
        public void Processing() => progress.Report(new AttachmentProgress.Processing(Id));

        /// <summary>The instance has it ready, as <paramref name="kind" />.</summary>
        public void Ready(MediaKind kind = MediaKind.Image) => _answer.TrySetResult(new PendingAttachment(Id, kind));

        /// <summary>The instance would not take it.</summary>
        public void Refuse(Exception refusal) => _answer.TrySetException(refusal);

        private static TaskCompletionSource<PendingAttachment> Registered(CancellationToken cancellationToken)
        {
            var answer = new TaskCompletionSource<PendingAttachment>();

            cancellationToken.Register(() => answer.TrySetCanceled(cancellationToken));

            return answer;
        }
    }
}
