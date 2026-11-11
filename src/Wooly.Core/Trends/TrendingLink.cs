namespace Wooly.Core.Trends;

/// <summary>
///     A <b>Trending link</b>, in GLOSSARY.md's word: an article from outside Mastodon that the instance ranks as shared
///     now. The one kind of trend that is a new value — a trending hashtag is a <see cref="Search.Hashtag" /> and a
///     trending post a <see cref="Posts.Post" /> — because what it is about lives outside Mastodon.
/// </summary>
/// <remarks>
///     It opens as the posts sharing it rather than as the article (ADR-0036), which is why <see cref="Url" /> is the
///     part everything else hangs on: it is both what the link timeline is read by and what the browser opens.
/// </remarks>
public sealed record TrendingLink
{
    /// <summary>Where the article is, as the instance knows it — the link timeline finds it by this alone.</summary>
    public required string Url { get; init; }

    /// <summary>What the article calls itself, or its address where the instance had no title for it.</summary>
    public required string Title { get; init; }

    /// <summary>What the article says of itself, or <see langword="null" /> where it said nothing.</summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Whose site it is on, as the site names itself ("The Guardian"), or <see langword="null" /> where it did not.
    /// </summary>
    public string? Site { get; init; }

    /// <summary>
    ///     How many people shared it over the days the instance reported, added up as a hashtag's
    ///     <see cref="Search.Hashtag.RecentAccounts" /> is — or zero where it said nothing.
    /// </summary>
    public required long PeopleSharing { get; init; }
}
