using System.Text.Json.Serialization;

namespace Wooly.Core.Notifications;

/// <summary>
///     What <c>/api/v1/markers</c> answers with, as far as notifications go: the marker, or nothing where the account
///     has never had one.
///     <para>
///         Written out here rather than read through Mastonet's own <c>GetMarkers</c>, because Mastonet 3.1.3 types
///         <c>last_read_id</c> as a number and Mastodon sends it as a string, as it sends every id — so the library's
///         call throws on every answer an instance gives. Read as a string, it is also the same kind of id as every
///         other id this client holds.
///     </para>
/// </summary>
internal sealed record MarkersWire
{
    /// <summary>The notifications marker, or <see langword="null" /> where the account has none.</summary>
    [JsonPropertyName("notifications")]
    public MarkerWire? Notifications { get; init; }

    /// <summary>One timeline's marker.</summary>
    internal sealed record MarkerWire
    {
        /// <summary>The id of the newest thing read on that timeline.</summary>
        [JsonPropertyName("last_read_id")]
        public string LastReadId { get; init; } = string.Empty;
    }
}
