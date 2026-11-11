using System.Text.Json.Serialization;

namespace Wooly.Core.Trends;

/// <summary>
///     What <c>/api/v1/trends/links</c> answers with for each link: Mastodon's preview card, with the daily usage a
///     trend carries. Written out here because Mastonet 3.1.3's <c>Card</c> has no <c>history</c>, and the usage is
///     the half of a trending link that says how much it is being talked about. Only the fields this client reads.
///     Nothing above <see cref="InstanceTrends" /> ever sees this record.
/// </summary>
internal sealed record TrendingLinkWire
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("provider_name")]
    public string? ProviderName { get; init; }

    /// <summary>One entry a day, newest first, its counts written as strings, as Mastodon writes them.</summary>
    [JsonPropertyName("history")]
    public IReadOnlyList<DayWire>? History { get; init; }

    /// <summary>
    ///     The link as this client holds it. Blank text is no text: an instance sends an empty string for a card that
    ///     had no description or no site, and a row drawn for one would be an empty row.
    /// </summary>
    public TrendingLink ToTrendingLink() => new()
    {
        Url = Url ?? string.Empty,
        Title = string.IsNullOrWhiteSpace(Title) ? Url ?? string.Empty : Title.Trim(),
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
        Site = string.IsNullOrWhiteSpace(ProviderName) ? null : ProviderName.Trim(),

        // A count this client cannot read counts as nobody rather than failing the read, as a hashtag's does: usage is
        // the least of what a trending link is for.
        PeopleSharing = History?.Sum(day => long.TryParse(day.Accounts, out var people) ? people : 0) ?? 0,
    };

    /// <summary>A day's usage of the link.</summary>
    internal sealed record DayWire
    {
        [JsonPropertyName("accounts")]
        public string? Accounts { get; init; }
    }
}
