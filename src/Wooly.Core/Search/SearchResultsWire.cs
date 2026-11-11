using System.Text.Json.Serialization;
using WireAccount = Mastonet.Entities.Account;
using WireStatus = Mastonet.Entities.Status;
using WireTag = Mastonet.Entities.Tag;

namespace Wooly.Core.Search;

/// <summary>
///     What <c>/api/v2/search</c> answers with, read raw since search left Mastonet (#599). The three lists hold
///     Mastonet's own entities, so that what was found crosses into this project through the same mappings every other
///     account, post and hashtag does — the shape <see cref="Notifications.GroupedNotificationsWire" /> set.
/// </summary>
/// <remarks>
///     Each list may be missing: an instance that found none of a kind, or was asked for another, can leave it out
///     rather than send it empty, and the adapter reads either as none found.
/// </remarks>
internal sealed record SearchResultsWire
{
    [JsonPropertyName("accounts")]
    public IReadOnlyList<WireAccount>? Accounts { get; init; }

    [JsonPropertyName("statuses")]
    public IReadOnlyList<WireStatus>? Statuses { get; init; }

    [JsonPropertyName("hashtags")]
    public IReadOnlyList<WireTag>? Hashtags { get; init; }
}
