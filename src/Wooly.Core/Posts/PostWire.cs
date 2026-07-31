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
        Boosted = status.Reblog is null ? null : ToPost(status.Reblog, instance),
        Url = status.Url,
    };

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
