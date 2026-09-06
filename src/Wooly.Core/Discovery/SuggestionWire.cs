using System.Text.Json.Serialization;
using WireAccount = Mastonet.Entities.Account;

namespace Wooly.Core.Discovery;

/// <summary>
///     What <c>/v2/suggestions</c> answers with: an account, and the sources it was offered under. Written out here
///     because Mastonet 3.1.3 calls <c>v1</c> only, whose entity carries a single <c>source</c> string and throws the
///     array away — which is the whole reason <see cref="FollowSuggestions" /> reads this endpoint by hand.
///     <para>
///         The account inside is Mastonet's own, so it crosses into this project's <see cref="Accounts.Account" />
///         through the one mapper every other account goes through. Nothing above <see cref="FollowSuggestions" />
///         ever sees this record.
///     </para>
/// </summary>
internal sealed record SuggestionWire
{
    /// <summary>
    ///     Why the instance is offering this account, in its own words for them. Nullable rather than defaulted,
    ///     because an instance may send the array absent or send it as a literal <c>null</c> and only one of those two
    ///     leaves a default standing — a suggestion that named no source is one offered for no reason this client will
    ///     name, not one to fall over.
    /// </summary>
    [JsonPropertyName("sources")]
    public IReadOnlyList<string>? Sources { get; init; }

    /// <summary>Who is being offered. Every suggestion has one — an entry without it is not a suggestion.</summary>
    [JsonPropertyName("account")]
    public WireAccount? Account { get; init; }
}
