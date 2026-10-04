using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using Wooly.Core.Http;
using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Reads an instance's post limits off its own description of itself (#319): <c>/api/v2/instance</c> where it has
///     one, and <c>/api/v1/instance</c> where it is older than that. Both by hand through <see cref="RawMastodonCall" />
///     rather than through Mastonet, whose 3.1.3 entities carry Mastodon's names for the limits and not the one Pleroma
///     and Akkoma answer with — so the call is retried, rate-limit-checked and reported like every other, and the one
///     place the answer is read can read all three.
///     <para>
///         Nothing here is caught but the one refusal that means "ask the other one": an instance with no <c>v2</c>
///         answers 404. Whether a failure is worth telling anybody about is the caller's decision.
///     </para>
/// </summary>
public sealed class InstanceLimits(IHttpClientFactory httpClientFactory) : IInstanceLimits
{
    /// <inheritdoc />
    public async Task<PostLimits> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        InstanceWire? described;

        try
        {
            described = await RawMastodonCall.Get<InstanceWire>(
                httpClientFactory,
                profile,
                "api/v2/instance",
                [],
                cancellationToken);
        }
        catch (HttpRequestException refused) when (refused.StatusCode == HttpStatusCode.NotFound)
        {
            described = await RawMastodonCall.Get<InstanceWire>(
                httpClientFactory,
                profile,
                "api/v1/instance",
                [],
                cancellationToken);
        }

        var statuses = described?.Configuration?.Statuses;

        return new PostLimits(
            statuses?.MaxCharacters ?? described?.MaxTootChars ?? PostLimits.Default.Characters,
            statuses?.CharactersReservedPerUrl ?? PostLimits.Default.PerAddress);
    }

    /// <summary>The little of an instance's description of itself this reads, under every name it goes by.</summary>
    private sealed record InstanceWire
    {
        [JsonPropertyName("configuration")]
        public ConfigurationWire? Configuration { get; init; }

        /// <summary>Pleroma's and Akkoma's name for the limit, on their <c>v1</c> answer.</summary>
        [JsonPropertyName("max_toot_chars")]
        public int? MaxTootChars { get; init; }
    }

    private sealed record ConfigurationWire
    {
        [JsonPropertyName("statuses")]
        public StatusesWire? Statuses { get; init; }
    }

    private sealed record StatusesWire
    {
        [JsonPropertyName("max_characters")]
        public int? MaxCharacters { get; init; }

        [JsonPropertyName("characters_reserved_per_url")]
        public int? CharactersReservedPerUrl { get; init; }
    }
}
