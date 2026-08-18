namespace Wooly.Core.Posts;

/// <summary>
///     One post, in this project's vocabulary rather than the API's (CONTEXT.md): a <c>status</c> is a post, a
///     <c>reblog</c> is a boost, and <c>favourites</c> are favorites. Nothing above this layer sees the wire names.
/// </summary>
public sealed record Post
{
    /// <summary>The instance's own id for the post, which is how anything else is asked about it.</summary>
    public required string Id { get; init; }

    /// <summary>Who posted it, as <c>username@instance</c>.</summary>
    public required string Account { get; init; }

    /// <summary>The name that account chose to be shown as, which is not unique and may be anything at all.</summary>
    public required string Author { get; init; }

    /// <summary>When it was posted, as UTC.</summary>
    public required DateTimeOffset PostedAt { get; init; }

    /// <summary>The post's text, with the API's HTML flattened to something a terminal can print.</summary>
    public required string Content { get; init; }

    /// <summary>
    ///     What the author put the post behind, or <see langword="null" /> if they put it behind nothing. Kept apart
    ///     from <see cref="Content" /> so a reader can honour the warning rather than print past it.
    /// </summary>
    public string? ContentWarning { get; init; }

    /// <summary>
    ///     Whether the instance marked what is attached to the post as something to hide until it is asked for — the
    ///     wire's own <c>sensitive</c> flag, which Mastodon carries apart from <see cref="ContentWarning" /> and which
    ///     a post commonly has without one: a photograph marked sensitive with nothing written over it (#113).
    /// </summary>
    /// <remarks>
    ///     Not <see langword="required" />, and false where the wire left it out — which is a field the client library
    ///     types as nullable rather than a question an instance declined to answer. Marked is the only thing that has
    ///     to be said out loud; anything else is a post with nothing to hide, which is most of them.
    /// </remarks>
    public bool Sensitive { get; init; }

    /// <summary>
    ///     Who can see it. Read back off a post rather than assumed, which is what lets a client confirm that the
    ///     post it just published went out as narrowly as it was asked to — an author who meant <c>private</c> and got
    ///     <c>public</c> has no way to undo that by the time they find out.
    /// </summary>
    public required PostVisibility Visibility { get; init; }

    /// <summary>How many accounts have boosted it.</summary>
    public required long Boosts { get; init; }

    /// <summary>How many accounts have favorited it.</summary>
    public required long Favorites { get; init; }

    /// <summary>How many replies it has drawn.</summary>
    public required long Replies { get; init; }

    /// <summary>
    ///     What the profile reading the post has already done to it. Distinct from the three counts above, which say
    ///     how many accounts did each thing without saying whether one of them was you — and a screen cannot draw a
    ///     lit star, or offer to take a boost back, without knowing that (ADR-0014).
    /// </summary>
    public required PostMarks Marks { get; init; }

    /// <summary>
    ///     What is attached to the post besides its text, in the order the author attached it, and empty where nothing
    ///     is. Read back off the instance, which is what makes it <see cref="PostMedia" /> rather than the
    ///     <see cref="MediaAttachment" /> a draft carries up.
    /// </summary>
    public IReadOnlyList<PostMedia> Media { get; init; } = [];

    /// <summary>
    ///     Everyone the post names, as <c>username@instance</c>, and empty where it names nobody. What the instance
    ///     itself resolved the handles in <see cref="Content" /> to — which is the only thing that says which
    ///     <c>@maria</c> a bare <c>@maria</c> is (#85).
    /// </summary>
    public IReadOnlyList<string> Mentions { get; init; } = [];

    /// <summary>
    ///     The post this one boosts, or <see langword="null" /> if it is not a boost. A boost carries no text of its
    ///     own — <see cref="Account" /> is who boosted, and everything worth reading is in here.
    /// </summary>
    public Post? Boosted { get; init; }

    /// <summary>Where to read it on the web, or <see langword="null" /> if the instance did not say.</summary>
    public string? Url { get; init; }

    /// <summary>
    ///     Where to read the author's avatar, or <see langword="null" /> if the instance did not say — the wire says
    ///     "no avatar" with an empty string, the same way it says "no warning" for <see cref="ContentWarning" />.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>The post this one answers, or <see langword="null" /> if it answers nothing.</summary>
    public PostReplyTarget? InReplyTo { get; init; }

    /// <summary>A poll attached to the post, or <see langword="null" /> if it carries none.</summary>
    public PostPoll? Poll { get; init; }

    /// <summary>
    ///     What the instance made of a link in the post's own text, or <see langword="null" /> where it previewed none.
    ///     Beside <see cref="Media" /> rather than among it, because this is generated by the instance from what the
    ///     author wrote rather than attached by the author besides it (ADR-0018).
    /// </summary>
    public LinkPreview? LinkPreview { get; init; }

    /// <summary>Whether this post is a boost of somebody else's.</summary>
    public bool IsBoost => Boosted is not null;

    /// <summary>
    ///     Whether the post is put behind something a reader has to ask past: a warning its author wrote, the flag the
    ///     instance marked its media with, or both.
    /// </summary>
    /// <remarks>
    ///     One question rather than two asked side by side, because the two halves are the same promise made in two
    ///     fields and a client that honoured one of them would keep half of it — which is what a picture marked
    ///     sensitive, drawn full width under no warning at all, was (#113). What each half hides is still its own:
    ///     the text stands behind <see cref="ContentWarning" /> alone, and everything else behind either.
    ///     <para>
    ///         "Everything else" is <see cref="Media" /> and <see cref="LinkPreview" /> both (#116). A link preview is
    ///         not something the author attached, but it commonly carries a picture the instance chose, and a picture
    ///         an instance flagged is the whole of what the flag is for — so a post flagged sensitive shows nothing of
    ///         one until it is asked for, whether or not anything is attached beside it.
    ///     </para>
    ///     <para>
    ///         The flag still counts for nothing on a post carrying neither, which an instance is free to send: with
    ///         nothing under it there is nothing behind anything and nothing for a reader to ask past. A warning's text
    ///         is not read that way — an author who wrote one wrote it about the words.
    ///     </para>
    /// </remarks>
    public bool IsWarned =>
        ContentWarning is not null || (Sensitive && (Media.Count > 0 || LinkPreview is not null));
}
