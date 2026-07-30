namespace Wooly.Core.Search;

/// <summary>
///     A hashtag a search turned up: the tag itself, and how much use it has had lately. The usage is what makes a
///     hashtag result worth reading rather than counting — a search for "cats" finds several near-identical tags, and
///     the one worth reading is the one people are actually posting to.
/// </summary>
/// <remarks>
///     The name is bare, exactly as <c>timeline tag</c> takes one, so a tag found here can be read without a user
///     having to know which spellings this client accepts.
/// </remarks>
public sealed record Hashtag
{
    /// <summary>The tag, without its leading <c>#</c>.</summary>
    public required string Name { get; init; }

    /// <summary>How many posts have carried it over the days the instance reported, or zero if it said nothing.</summary>
    public required long RecentPosts { get; init; }

    /// <summary>How many accounts those posts came from, or zero if the instance said nothing.</summary>
    public required long RecentAccounts { get; init; }

    /// <summary>Where to read it on the web, or <see langword="null" /> if the instance did not say.</summary>
    public string? Url { get; init; }
}
