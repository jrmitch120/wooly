using HashtagRule = Wooly.Core.Timelines.Hashtag;
using WireHashtag = Mastonet.Entities.Tag;
using WireHistory = Mastonet.Entities.History;

namespace Wooly.Core.Search;

/// <summary>
///     The one crossing between Mastodon's <c>tag</c> and this project's <see cref="Hashtag" />, alongside
///     <see cref="Posts.PostWire" /> and <see cref="Accounts.AccountWire" /> and for the same reason: one mapping, so a
///     hashtag looks the same however it arrived.
/// </summary>
internal static class SearchWire
{
    public static Hashtag ToHashtag(WireHashtag tag) => new()
    {
        // Put through the same rule the tag timeline is read by, so a tag a search turned up is one `timeline tag`
        // will take — an instance that answered with a leading # would otherwise hand back a tag this client rejects.
        Name = HashtagRule.Bare(tag.Name),
        RecentPosts = Total(tag, history => history.Uses),
        RecentAccounts = Total(tag, history => history.Accounts),
        Url = tag.Url,
    };

    /// <summary>
    ///     Adds up one column of the daily usage an instance sends with a tag. The counts arrive as strings, because
    ///     that is how Mastodon writes them, and one it wrote in a way this client cannot read counts as nothing rather
    ///     than failing the whole search — usage is the least of what a hashtag result is for.
    /// </summary>
    private static long Total(WireHashtag tag, Func<WireHistory, string?> column) =>
        tag.History?.Sum(day => long.TryParse(column(day), out var count) ? count : 0) ?? 0;
}
