using System.Text.Json.Serialization;

namespace Wooly.Core.Posts;

/// <summary>
///     What <c>POST /api/v1/statuses/:id/translate</c> answers with — Mastodon's <c>Translation</c> entity, as 4.2 and
///     later send it. Written out here because Mastonet 3.1.3 has none (ADR-0037). Only the fields this client reads;
///     nothing above <see cref="PostTranslation" /> ever sees this record.
/// </summary>
internal sealed record TranslationWire
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>The warning as plain text, the instance having already taken the markup off it; empty where none.</summary>
    [JsonPropertyName("spoiler_text")]
    public string? SpoilerText { get; init; }

    [JsonPropertyName("poll")]
    public PollWire? Poll { get; init; }

    [JsonPropertyName("media_attachments")]
    public IReadOnlyList<MediaWire>? MediaAttachments { get; init; }

    [JsonPropertyName("detected_source_language")]
    public string? DetectedSourceLanguage { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    ///     The translation as this client holds it. Blank is nothing, as everywhere the wire says "none" with an empty
    ///     string: a warning the post never had, an attachment nobody described.
    /// </summary>
    public Translation ToTranslation() => new()
    {
        Content = InstanceHtml.ToPlainText(Content),
        ContentWarning = MastodonWire.SaidOrNothing(SpoilerText),
        PollId = Poll?.Id,
        PollOptions = Poll?.Options?.Select(option => option.Title ?? string.Empty).ToList() ?? [],
        Descriptions = (MediaAttachments ?? [])
                       .Where(attached => attached.Id is not null
                                          && MastodonWire.SaidOrNothing(attached.Description) is not null)
                       .ToDictionary(attached => attached.Id!, attached => attached.Description!),
        From = MastodonWire.SaidOrNothing(DetectedSourceLanguage),
        To = Language ?? string.Empty,
        Provider = Provider ?? string.Empty,
    };

    /// <summary>The poll's options translated, matched to the poll by its id: the wire keeps none on an option.</summary>
    internal sealed record PollWire
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("options")]
        public IReadOnlyList<OptionWire>? Options { get; init; }
    }

    internal sealed record OptionWire
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }
    }

    /// <summary>One attachment's description translated, and the id of the attachment it describes.</summary>
    internal sealed record MediaWire
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }
    }
}
