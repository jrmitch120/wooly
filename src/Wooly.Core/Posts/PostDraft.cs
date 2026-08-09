namespace Wooly.Core.Posts;

/// <summary>
///     A post that has not been published yet: everything an author composed, in one value. A reply is not a separate
///     kind of thing — it is a draft that names the post it answers (<see cref="InReplyTo" />), which is what lets
///     replying offer exactly what posting offers rather than a subset somebody has to keep in step.
/// </summary>
public sealed record PostDraft
{
    /// <summary>
    ///     The post's own text. May be empty, but only for a draft carrying media — see <see cref="Problem" />.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    ///     What to put the post behind, or <see langword="null" /> to put it behind nothing. An instance treats an
    ///     empty string as no warning at all, so nothing but <see langword="null" /> is used to say that here.
    /// </summary>
    public string? ContentWarning { get; init; }

    /// <summary>
    ///     Who should be able to see it, or <see langword="null" /> to leave the choice to the account's own default on
    ///     the instance. Null is not a synonym for public: an account whose default is followers-only would have every
    ///     post from this client published wider than the account asked for, which is not a mistake an author can take
    ///     back.
    /// </summary>
    public PostVisibility? Visibility { get; init; }

    /// <summary>
    ///     Where <see cref="Visibility" /> came from: <see langword="true" /> if this composition named it,
    ///     <see langword="false" /> if it is a standing preference the composer had nothing to say about.
    /// </summary>
    /// <remarks>
    ///     The difference only matters for a reply, which may not go out wider than the post it answers (ADR-0013). A
    ///     preference too wide for the post being answered is narrowed to fit without comment — otherwise a profile
    ///     whose default is public could never answer a direct message. A visibility named on the invocation itself is
    ///     refused instead, because publishing something other than what was asked for is not a thing to do quietly.
    /// </remarks>
    public bool VisibilityChosen { get; init; }

    /// <summary>
    ///     The id of the post this one answers, or <see langword="null" /> if it answers nothing.
    /// </summary>
    public string? InReplyTo { get; init; }

    /// <summary>Files to attach, in the order they should appear on the post.</summary>
    public IReadOnlyList<MediaAttachment> Media { get; init; } = [];

    /// <summary>A poll to attach, or <see langword="null" /> for a post that asks nothing.</summary>
    public PollDraft? Poll { get; init; }

    /// <summary>
    ///     What is wrong with this draft, or <see langword="null" /> if nothing is. The one place the rule lives, asked
    ///     twice for the reason <see cref="PollDraft.Problem" /> gives: once by the argument parser, so the user reads
    ///     the answer where they typed the mistake, and once by the adapter, so a draft that reaches an instance is one
    ///     it can be asked to publish.
    /// </summary>
    public string? Problem
    {
        get
        {
            // Mastodon takes a post with no text only when it carries media — a picture is the thing being said. It
            // will not take one that is nothing but a poll, so a question still has to be asked in words.
            if (string.IsNullOrWhiteSpace(Text) && Media.Count == 0)
            {
                return "A post needs something to say: give it text, or attach a file.";
            }

            // An instance stores one or the other on a post, and refuses a request carrying both. Answered here rather
            // than left to the refusal, because by then the media has already been uploaded.
            return Media.Count > 0 && Poll is not null
                ? "A post carries either files or a poll, not both."
                : null;
        }
    }
}
