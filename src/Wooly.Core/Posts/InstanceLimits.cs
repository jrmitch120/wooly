using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wooly.Core.Errors;
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
///         An instance with no <c>v2</c> answers 404, which means "ask the other one". Every other way of not
///         answering is silence, as the port promises; a refused token is left to be said elsewhere.
///     </para>
/// </summary>
public sealed class InstanceLimits(IHttpClientFactory httpClientFactory) : IInstanceLimits
{
    /// <inheritdoc />
    public async Task<PostLimits?> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        InstanceWire? described;

        try
        {
            try
            {
                described = await Described("api/v2/instance");
            }
            catch (HttpRequestException refused) when (refused.StatusCode == HttpStatusCode.NotFound)
            {
                described = await Described("api/v1/instance");
            }
        }
        // Each of these is the instance not answering, which the port promises as silence. A timeout is the HTTP
        // stack cancelling a call nobody asked to cancel, and only that: a cancellation the caller asked for is still
        // the caller's.
        catch (Exception unanswered) when (unanswered is RateLimitedException
                                               or TransientNetworkException
                                               or HttpRequestException
                                               or JsonException
                                           || (unanswered is OperationCanceledException
                                               && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }

        var statuses = described?.Configuration?.Statuses;

        return new PostLimits(
            statuses?.MaxCharacters ?? described?.MaxTootChars ?? PostLimits.Default.Characters,
            statuses?.CharactersReservedPerUrl ?? PostLimits.Default.PerAddress);

        Task<InstanceWire?> Described(string path) =>
            RawMastodonCall.Get<InstanceWire>(httpClientFactory, profile, path, [], cancellationToken);
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
