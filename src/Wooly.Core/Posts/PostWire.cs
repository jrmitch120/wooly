using Mastonet;
using Mastonet.Entities;

namespace Wooly.Core.Posts;

/// <summary>
///     The one crossing between Mastodon's <c>status</c> and this project's <see cref="Post" /> (CONTEXT.md). Shared by
///     everything that gets a post back from an instance — reading a timeline, publishing one, editing one — so that a
///     post looks the same however it arrived. Two ways of mapping the same wire type is how a field comes to be filled
///     in on a timeline and empty on the post that was just published.
/// </summary>
internal static class PostWire
{
    /// <param name="instance">
    ///     The instance being read, needed because it names its own accounts by bare username and everyone else's in
    ///     full.
    /// </param>
    public static Post ToPost(Status status, string instance) => new()
    {
        Id = status.Id,
        Account = MastodonWire.Qualify(status.Account, instance),
        Author = MastodonWire.DisplayName(status.Account),
        PostedAt = MastodonWire.AsUtc(status.CreatedAt),
        Content = PostContent.ToPlainText(status.Content),

        // The wire says "no warning" with an empty string, which is not the same thing as a warning to print.
        ContentWarning = string.IsNullOrWhiteSpace(status.SpoilerText) ? null : status.SpoilerText,

        // The other half of what a post is put behind, and the half with no text to read it off: an instance marks
        // media sensitive on its own account, and most often with no warning written at all (#113). Nullable on the
        // way in because that is how the client library types the field, and nothing said is nothing marked.
        Sensitive = status.Sensitive ?? false,
        Visibility = ToVisibility(status.Visibility),
        Boosts = status.ReblogCount,
        Favorites = status.FavouritesCount,
        Replies = status.RepliesCount,

        // The wire leaves all three out where it has nobody to answer them about, which is a read made without a
        // token. Silence there means "not marked" rather than "unknown": every call this client makes is signed in,
        // so a missing flag is an instance that had nothing to report rather than one that was not asked.
        Marks = new PostMarks
        {
            Boosted = status.Reblogged ?? false,
            Favorited = status.Favourited ?? false,
            Pinned = status.Pinned ?? false,
        },
        // Mastonet leaves this null rather than empty where a post carries nothing, and a timeline is mostly posts
        // that carry nothing.
        Media = status.MediaAttachments?.Select(ToMedia).ToList() ?? [],

        // Qualified the same way the post's own author is, and for the same reason: an instance names its own
        // accounts bare, so a mention left as it arrived would say who it is about in a different way on every second
        // post.
        Mentions = status.Mentions?.Select(mention => MastodonWire.Qualify(mention.AccountName, instance)).ToList()
                   ?? [],
        Boosted = status.Reblog is null ? null : ToPost(status.Reblog, instance),
        Url = status.Url,

        // The wire says "no avatar" with an empty string, the same as it says "no warning" above.
        AvatarUrl = string.IsNullOrWhiteSpace(status.Account.AvatarUrl) ? null : status.Account.AvatarUrl,
        InReplyTo = ToReplyTarget(status, instance),
        Poll = status.Poll is null ? null : ToPoll(status.Poll),
        LinkPreview = ToLinkPreview(status.Card),
    };

    /// <summary>
    ///     What the instance made of a link in the post's text, trimmed to what a terminal can use, or
    ///     <see langword="null" /> where it previewed nothing (ADR-0018).
    /// </summary>
    /// <remarks>
    ///     A card with no address of its own is read as no preview at all rather than as a preview that cannot be
    ///     opened: its address is the whole reason it is a <c>Reference</c>, and a title with nowhere to press
    ///     <c>⏎</c> is enrichment this client has no way to offer.
    /// </remarks>
    private static LinkPreview? ToLinkPreview(Card? card)
    {
        if (card is null || string.IsNullOrWhiteSpace(card.Url))
        {
            return null;
        }

        return new LinkPreview
        {
            Url = card.Url,

            // The wire says "nothing made of this" with an empty string, the same way it says "no warning" above —
            // every one of these is a field an instance sends blank rather than leaves out.
            Title = Said(card.Title),
            Description = Said(card.Description),
            ProviderName = Said(card.ProviderName),
            Image = Said(card.Image),
            Author = Said(card.AuthorName),
        };
    }

    /// <summary>What the wire said, or <see langword="null" /> where what it said was nothing.</summary>
    private static string? Said(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    ///     What a reply answers, or <see langword="null" /> for a post that answers nothing. A self-reply's handle is
    ///     read off the post's own account, since Mastodon does not list an author among their own post's mentions;
    ///     any other answered account is read off <see cref="Status.Mentions" />, which is where an instance lists
    ///     everyone a post names — the account it answers included, unless that account is only reachable by an id the
    ///     post never names.
    /// </summary>
    private static PostReplyTarget? ToReplyTarget(Status status, string instance)
    {
        if (status.InReplyToId is not { } postId)
        {
            return null;
        }

        if (status.InReplyToAccountId == status.Account.Id)
        {
            return new PostReplyTarget { PostId = postId, Handle = MastodonWire.Qualify(status.Account, instance) };
        }

        var answered = status.Mentions.FirstOrDefault(mention => mention.Id == status.InReplyToAccountId);

        return new PostReplyTarget
        {
            PostId = postId,
            Handle = answered is null ? null : MastodonWire.Qualify(answered.AccountName, instance),
        };
    }

    /// <summary>The poll as it stands, options and all — the read-side counterpart to what a draft sends up.</summary>
    /// <remarks>
    ///     Reachable on its own as well as through <see cref="ToPost" />, because a vote is answered with the poll
    ///     alone: Mastodon hands back the whole poll as it now stands and nothing of the post around it, and that
    ///     answer is grafted onto the post the caller already holds (<see cref="PostEngagement.Vote" />).
    /// </remarks>
    public static PostPoll ToPoll(Poll poll)
    {
        var picked = poll.OwnVotes.ToHashSet();

        return new PostPoll
        {
            Id = poll.Id,
            Options = poll.Options.Select((option, index) => new PostPollOption
            {
                Text = option.Title,
                Votes = option.VotesCount,
                Picked = picked.Contains(index),
            }).ToList(),
            Votes = poll.VotesCount,
            Voters = poll.VotersCount,
            MultipleChoice = poll.Multiple,
            Closed = poll.Expired,
            ExpiresAt = poll.ExpiresAt is { } expires ? MastodonWire.AsUtc(expires) : null,

            // The wire leaves this null where a read has nobody to answer it about, the same silence Marks treats as
            // "not voted" rather than "unknown" — every call this client makes is signed in.
            Voted = poll.Voted ?? false,
        };
    }

    /// <summary>One attachment as it came down, which is a different thing from one on its way up (<see cref="PostMedia" />).</summary>
    private static PostMedia ToMedia(Attachment attachment) => new()
    {
        Id = attachment.Id,
        Kind = ToKind(attachment.Type),
        Url = attachment.Url,
        Preview = string.IsNullOrWhiteSpace(attachment.PreviewUrl) ? null : attachment.PreviewUrl,

        // The wire says "described as nothing" with an empty string, which is not the same thing as a description to
        // read out.
        Description = string.IsNullOrWhiteSpace(attachment.Description) ? null : attachment.Description,
    };

    /// <summary>
    ///     What this client calls the kind the instance named. Anything else is kept as
    ///     <see cref="MediaKind.Unknown" /> rather than refused, because an instance is free to serve a kind newer than
    ///     this client and a post whose attachment cannot be named still has one.
    /// </summary>
    private static MediaKind ToKind(string? type) => type switch
    {
        "image" => MediaKind.Image,
        "gifv" => MediaKind.Animation,
        "video" => MediaKind.Video,
        "audio" => MediaKind.Audio,
        _ => MediaKind.Unknown,
    };

    /// <summary>How this project spells the visibility Mastonet handed back.</summary>
    /// <remarks>
    ///     Written out rather than cast, even though the two enums happen to list the same four in the same order.
    ///     A cast would tie this client's meaning of <c>2</c> to a number in somebody else's library, and a release
    ///     that inserted a fifth member would silently turn every private post public.
    /// </remarks>
    public static PostVisibility ToVisibility(Visibility visibility) => visibility switch
    {
        Visibility.Public => PostVisibility.Public,
        Visibility.Unlisted => PostVisibility.Unlisted,
        Visibility.Private => PostVisibility.Private,
        Visibility.Direct => PostVisibility.Direct,
        _ => throw new ArgumentOutOfRangeException(
            nameof(visibility),
            visibility,
            "Not a visibility this client knows."),
    };

    /// <summary>How the wire spells the visibility this client was asked for.</summary>
    /// <remarks>Written out for the reason <see cref="ToVisibility" /> gives, in the direction that publishes a post.</remarks>
    public static Visibility ToWire(PostVisibility visibility) => visibility switch
    {
        PostVisibility.Public => Visibility.Public,
        PostVisibility.Unlisted => Visibility.Unlisted,
        PostVisibility.Private => Visibility.Private,
        PostVisibility.Direct => Visibility.Direct,
        _ => throw new ArgumentOutOfRangeException(
            nameof(visibility),
            visibility,
            "Not a visibility this client knows."),
    };
}
