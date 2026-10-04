using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Reads an account's own defaults off <c>/api/v1/accounts/verify_credentials</c>'s <c>source</c> (#339), the one
///     place Mastodon says them, and only to the account itself. By hand through <see cref="RawMastodonCall" />, as
///     <see cref="InstanceLimits" /> is, so that the two fields are read as plain words: an instance on a fork can
///     answer with a privacy Mastodon has no name for, which is a default this client does not know rather than an
///     answer it cannot read at all. The call is still retried, rate-limit-checked and reported like every other.
/// </summary>
public sealed class AccountDefaults(IHttpClientFactory httpClientFactory) : IAccountDefaults
{
    /// <inheritdoc />
    public async Task<PostDefaults> Read(ActiveProfile profile, CancellationToken cancellationToken)
    {
        SourceWire? source;

        try
        {
            source = (await RawMastodonCall.Get<CredentialsWire>(
                httpClientFactory,
                profile,
                "api/v1/accounts/verify_credentials",
                [],
                cancellationToken))?.Source;
        }
        // Each of these is the instance not answering, which the port promises as unknown. A timeout is the HTTP
        // stack cancelling a call nobody asked to cancel, and only that: a cancellation the caller asked for is still
        // the caller's.
        catch (Exception unanswered) when (unanswered is RateLimitedException
                                               or TransientNetworkException
                                               or HttpRequestException
                                               or JsonException
                                           || (unanswered is OperationCanceledException
                                               && !cancellationToken.IsCancellationRequested))
        {
            return PostDefaults.Unknown;
        }

        return new PostDefaults(ToVisibility(source?.Privacy), PostLanguageName.Of(source?.Language ?? "")?.Code);
    }

    /// <summary>The wire's word for a visibility, with Mastodon's <c>private</c> read as followers (ADR-0024).</summary>
    private static PostVisibility? ToVisibility(string? privacy) => privacy switch
    {
        "public" => PostVisibility.Public,
        "unlisted" => PostVisibility.Unlisted,
        "private" => PostVisibility.Followers,
        "direct" => PostVisibility.Direct,
        _ => null,
    };

    /// <summary>The little of the account's own description of itself this reads.</summary>
    private sealed record CredentialsWire
    {
        [JsonPropertyName("source")]
        public SourceWire? Source { get; init; }
    }

    private sealed record SourceWire
    {
        [JsonPropertyName("privacy")]
        public string? Privacy { get; init; }

        [JsonPropertyName("language")]
        public string? Language { get; init; }
    }
}
