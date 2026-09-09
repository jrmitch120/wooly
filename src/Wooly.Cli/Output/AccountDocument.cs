using System.Text.Json.Serialization;
using Wooly.Core.Accounts;

namespace Wooly.Cli.Output;

/// <summary>
///     One account, written the one way every command writes an account — <see cref="PostDocument" /> for accounts, and
///     for the reason ADR-0011 gave: an account found by a search and the same account in a followers list have to read
///     alike, or a script needs a different filter for each command that turned one up.
///     <para>
///         That is why the six facts ADR-0019 put on <see cref="Account" /> are written on every account rather than
///         only by <c>account show</c>: noise is cheap in a pipe, and one spelling everywhere costs less than a bio a
///         script can only reach through one command.
///     </para>
/// </summary>
/// <param name="Address">
///     Written as <c>account</c>, which is what a post calls the same fact, so one <c>jq</c> filter reads an account's
///     address wherever it turns up.
/// </param>
/// <param name="Bio">
///     What the account wrote about itself, as the plain text a terminal can print. Written on every account, empty
///     included: a bio rides on every account entity Mastodon sends, so an empty one is somebody who wrote nothing
///     rather than a question that went unput — which is the whole of what tells these apart from <see cref="Standing" />.
/// </param>
/// <param name="Joined">
///     The day the account was created, as an ISO date. A day rather than an instant, because nothing about an account
///     measures its age in hours.
/// </param>
/// <param name="IsLocked">Whether the account approves its followers by hand, written as <c>locked</c>.</param>
/// <param name="IsBot">Whether it says it posts automatically rather than by hand, written as <c>bot</c>.</param>
/// <param name="Posts">How many posts the account has published — not a list of posts.</param>
/// <param name="AvatarUrl">
///     Where its avatar is, or absent where the instance did not say. The one of the six that can be absent, because it
///     is an address rather than something written — there is no avatar an account wrote as blank.
/// </param>
/// <param name="Standing">
///     Where the profile's own account stands with this one, or absent where the instance was not asked. Nested rather
///     than spread across the top level, because <c>following</c> already means how many accounts this one follows, and
///     one field cannot be both a count and a yes-or-no.
/// </param>
internal sealed record AccountDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("account")] string Address,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("bio")] string Bio,
    [property: JsonPropertyName("fields")] IReadOnlyList<AccountFieldDocument> Fields,
    [property: JsonPropertyName("joined")] DateOnly Joined,
    [property: JsonPropertyName("locked")] bool IsLocked,
    [property: JsonPropertyName("bot")] bool IsBot,
    [property: JsonPropertyName("followers")] long Followers,
    [property: JsonPropertyName("following")] long Following,
    [property: JsonPropertyName("posts")] long Posts,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("avatar")] string? AvatarUrl,
    [property: JsonPropertyName("standing")] StandingDocument? Standing)
{
    public static AccountDocument Of(Account account) => new(
        account.Id,
        account.Address,
        account.Author,
        account.Bio,
        [.. account.Fields.Select(AccountFieldDocument.Of)],
        account.Joined,
        account.IsLocked,
        account.IsBot,
        account.Followers,
        account.Following,
        account.Posts,
        account.Url,
        account.AvatarUrl,
        StandingDocument.Of(account.Standing));
}

/// <summary>One of the rows an account set under its bio, as another program reads it.</summary>
/// <param name="Label">What the account calls the row.</param>
/// <param name="Said">
///     What the row says, flattened out of the HTML the instance serves it as — this project's own word for it, so the
///     document and the domain spell a field the one way.
/// </param>
/// <param name="Verified">
///     When the instance proved the address the row carries belongs to the account, or absent for a row nobody proved.
///     The moment rather than a yes-or-no, because the wire says <em>when</em> and a bool would throw that away; absent
///     rather than null, since a field nobody proved is not a field anybody disproved.
/// </param>
internal sealed record AccountFieldDocument(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("said")] string Said,
    [property: JsonPropertyName("verified")] DateTimeOffset? Verified)
{
    public static AccountFieldDocument Of(AccountField field) => new(field.Label, field.Said, field.Verified);
}

/// <param name="Following">Whether the profile follows the account.</param>
/// <param name="Requested">Whether a follow is waiting for the account to accept it.</param>
/// <param name="FollowedBy">Whether the account follows the profile, which is the other direction.</param>
/// <param name="Note">
///     What the profile wrote to itself about the account, or absent where it wrote nothing. Here rather than on the
///     account because it is a fact about the pair — two profiles reading the same account read different notes — and
///     because Mastodon sends it only where it sends the rest of a standing.
/// </param>
internal sealed record StandingDocument(
    [property: JsonPropertyName("following")] bool Following,
    [property: JsonPropertyName("requested")] bool Requested,
    [property: JsonPropertyName("followedBy")] bool FollowedBy,
    [property: JsonPropertyName("blocking")] bool Blocking,
    [property: JsonPropertyName("muting")] bool Muting,
    [property: JsonPropertyName("note")] string? Note)
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
            standing.Muting,
            standing.Note);
}
