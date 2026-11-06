using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Reads the custom emoji an instance offers off <c>/api/v1/custom_emojis</c> (#578), by hand through
///     <see cref="RawMastodonCall" /> as <see cref="InstanceLimits" /> reads its limits, so the call is retried,
///     rate-limit-checked and reported like every other, and a refusal arrives as the same exception the limits read
///     already treats as silence. Only the shortcode, the still and whether the picker shows it are read: neither the
///     category nor the animated address is anything Wooly uses (ADR-0035).
/// </summary>
public sealed class InstanceEmoji(IHttpClientFactory httpClientFactory) : IInstanceEmoji
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomEmoji>?> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        IReadOnlyList<EmojiWire?>? listed;

        try
        {
            listed = await RawMastodonCall.Get<IReadOnlyList<EmojiWire?>>(
                httpClientFactory,
                profile,
                "api/v1/custom_emojis",
                [],
                cancellationToken);
        }
        // Each of these is the instance not answering, which the port promises as silence; as in InstanceLimits, a
        // timeout is the HTTP stack cancelling a call nobody asked to cancel, and only that.
        catch (Exception unanswered) when (unanswered is RateLimitedException
                                               or TransientNetworkException
                                               or HttpRequestException
                                               or JsonException
                                           || (unanswered is OperationCanceledException
                                               && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }

        // A bare null is no list, which is no answer either. Only an emoji the instance says its picker shows is kept:
        // one that leaves it out has not said it is offered.
        return listed?
            .OfType<EmojiWire>()
            .Where(emoji => emoji.VisibleInPicker == true
                            && !string.IsNullOrWhiteSpace(emoji.Shortcode)
                            && !string.IsNullOrWhiteSpace(emoji.StaticUrl))
            .Select(emoji => new CustomEmoji(emoji.Shortcode!, emoji.StaticUrl!))
            .ToList();
    }

    /// <summary>The little of one listed emoji this reads.</summary>
    private sealed record EmojiWire
    {
        [JsonPropertyName("shortcode")]
        public string? Shortcode { get; init; }

        [JsonPropertyName("static_url")]
        public string? StaticUrl { get; init; }

        [JsonPropertyName("visible_in_picker")]
        public bool? VisibleInPicker { get; init; }
    }
}
