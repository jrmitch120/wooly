namespace Wooly.Core.Accounts;

/// <summary>
///     One Mastodon account, as an instance describes it: who they are, where to read them, and how much of a presence
///     they have. Distinct from a local <see cref="Profiles.ProfileSummary" />, which is this client's own credential
///     entry pointing at one of these (CONTEXT.md).
///     <para>
///         The two facts a post already carries about its author — the address and the name shown — are named here with
///         the same two words a post names them with, so that <c>account</c> and <c>author</c> mean the same thing
///         wherever either turns up.
///     </para>
/// </summary>
public sealed record Account
{
    /// <summary>
    ///     The instance's own id for the account, which is what its endpoints take and what a pending follow request is
    ///     named by. Not the address: an id means nothing on any other instance, and nothing outside this client's own
    ///     output asks a user to type one.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Where the account is addressed, as <c>username@instance</c>, the same way a post names its author.</summary>
    public required string Address { get; init; }

    /// <summary>The name that account chose to be shown as, which is not unique and may be anything at all.</summary>
    public required string Author { get; init; }

    /// <summary>How many accounts follow it.</summary>
    public required long Followers { get; init; }

    /// <summary>How many accounts it follows.</summary>
    public required long Following { get; init; }

    /// <summary>How many posts it has published.</summary>
    public required long Posts { get; init; }

    /// <summary>Where to read it on the web, or <see langword="null" /> if the instance did not say.</summary>
    public string? Url { get; init; }

    /// <summary>
    ///     Where the profile's own account stands with this one, or <see langword="null" /> where the instance was not
    ///     asked. Absent is not the same as nothing: a search result and a followers list say nothing about standing
    ///     because Mastodon does not send it there, and five falses would say the profile follows none of the accounts
    ///     it is reading its own following list from.
    /// </summary>
    public AccountStanding? Standing { get; init; }
}
