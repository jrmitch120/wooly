using System.Net.Http;
using System.Text.Json.Serialization;
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
        // The instance not answering, which the port promises as unknown.
        catch (Exception unanswered) when (RawMastodonCall.Unanswered(unanswered, cancellationToken))
        {
            return PostDefaults.Unknown;
        }

        return new PostDefaults(
            PostWire.ToVisibility(source?.Privacy),
            PostLanguageName.Of(source?.Language ?? "")?.Code);
    }

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
