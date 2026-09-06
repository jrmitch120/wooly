using System.Text.Json.Serialization;
using WireAccount = Mastonet.Entities.Account;

namespace Wooly.Core.Relationships;

/// <summary>
///     What <c>/accounts/familiar_followers</c> answers with: one entry per id it was asked about, each naming that id
///     and the accounts in common. The wire's own envelope rather than a record of this client's — familiar followers
///     need no record, being accounts, and nothing above <see cref="AccountRelationships" /> ever sees this.
///     <para>
///         Written out here only because Mastonet 3.1.3 has no entity for an endpoint it does not call. The accounts
///         inside are Mastonet's own, so they cross into this project's <see cref="Accounts.Account" /> through the one
///         mapper every other account goes through.
///     </para>
/// </summary>
internal sealed record FamiliarFollowersWire
{
    /// <summary>The account that was asked about, which the endpoint echoes back beside its answer.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Who the profile follows that also follows <see cref="Id" />, and empty where that is nobody.</summary>
    [JsonPropertyName("accounts")]
    public IReadOnlyList<WireAccount> Accounts { get; init; } = [];
}
