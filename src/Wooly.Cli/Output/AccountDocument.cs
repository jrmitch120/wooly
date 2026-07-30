using System.Text.Json.Serialization;
using Wooly.Core.Accounts;

namespace Wooly.Cli.Output;

/// <summary>
///     One account, written the one way every command writes an account — <see cref="PostDocument" /> for accounts, and
///     for the reason ADR-0011 gave: an account found by a search and the same account in a followers list have to read
///     alike, or a script needs a different filter for each command that turned one up.
/// </summary>
/// <param name="Address">
///     Written as <c>account</c>, which is what a post calls the same fact, so one <c>jq</c> filter reads an account's
///     address wherever it turns up.
/// </param>
/// <param name="Posts">How many posts the account has published — not a list of posts.</param>
/// <param name="Standing">
///     Where the profile's own account stands with this one, or absent where the instance was not asked. Nested rather
///     than spread across the top level, because <c>following</c> already means how many accounts this one follows, and
///     one field cannot be both a count and a yes-or-no.
/// </param>
internal sealed record AccountDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("account")] string Address,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("followers")] long Followers,
    [property: JsonPropertyName("following")] long Following,
    [property: JsonPropertyName("posts")] long Posts,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("standing")] StandingDocument? Standing)
{
    public static AccountDocument Of(Account account) => new(
        account.Id,
        account.Address,
        account.Author,
        account.Followers,
        account.Following,
        account.Posts,
        account.Url,
        StandingDocument.Of(account.Standing));
}

/// <param name="Following">Whether the profile follows the account.</param>
/// <param name="Requested">Whether a follow is waiting for the account to accept it.</param>
/// <param name="FollowedBy">Whether the account follows the profile, which is the other direction.</param>
internal sealed record StandingDocument(
    [property: JsonPropertyName("following")] bool Following,
    [property: JsonPropertyName("requested")] bool Requested,
    [property: JsonPropertyName("followedBy")] bool FollowedBy,
    [property: JsonPropertyName("blocking")] bool Blocking,
    [property: JsonPropertyName("muting")] bool Muting)
{
    /// <summary>
    ///     The standing as JSON, or <see langword="null" /> where the instance was not asked — which is left out of the
    ///     document altogether rather than written as five falses a script would read as "none of these are true".
    /// </summary>
    public static StandingDocument? Of(AccountStanding? standing) => standing is null
        ? null
        : new StandingDocument(
            standing.Following,
            standing.FollowRequested,
            standing.FollowedBy,
            standing.Blocking,
            standing.Muting);
}
