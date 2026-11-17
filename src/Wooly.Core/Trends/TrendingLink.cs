namespace Wooly.Core.Trends;

/// <summary>
///     A <b>Trending link</b>, in GLOSSARY.md's word: an article from outside Mastodon that the instance ranks as shared
///     now. The one kind of trend that is a new value — a trending hashtag is a <see cref="Search.Hashtag" /> and a
///     trending post a <see cref="Posts.Post" /> — because what it is about lives outside Mastodon.
/// </summary>
/// <remarks>
///     It opens as the posts sharing it rather than as the article (ADR-0036), which is why <see cref="Url" /> is the
///     part everything else hangs on: it is both what the link timeline is read by and what the browser opens. A web
///     address in full, always (<see cref="Timelines.SharedLink" />): a card the instance sent with anything else is
///     dropped where it arrives, so nothing that holds a link has to ask again whether it can be opened (#598's review).
/// </remarks>
public sealed record TrendingLink
{
    /// <summary>
    ///     Where the article is, as the instance knows it and as it wrote it (<see cref="Uri.OriginalString" />) — the
    ///     link timeline finds it by this alone.
    /// </summary>
    public required Uri Url { get; init; }

    /// <summary>What the article calls itself, or its address where the instance had no title for it.</summary>
    public required string Title { get; init; }

    /// <summary>What the article says of itself, or <see langword="null" /> where it said nothing.</summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Whose site it is on, as the site names itself ("The Guardian"), or by the host it is on where it did not — a
    ///     reader judging a link by its site and how many people are sharing it (#598's sixteenth story).
    /// </summary>
    public required string Site { get; init; }

    /// <summary>
    ///     How many people shared it over the days the instance reported, added up as a hashtag's
    ///     <see cref="Search.Hashtag.RecentAccounts" /> is — or zero where it said nothing.
    /// </summary>
    public required long PeopleSharing { get; init; }
}
