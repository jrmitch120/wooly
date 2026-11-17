using System.Net;
using System.Net.Http.Json;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Profiles;

namespace Wooly.Core.Posts;

/// <summary>
///     Asks the instance's translate endpoint, and its languages map (#616), raw, through
///     <see cref="RawMastodonCall" /> over the named <see cref="HttpClient" /> every Mastonet call runs through, so a
///     translation is retried, rate-limit-checked and authorized like any other call — Mastonet 3.1.3 having no
///     translation at all (ADR-0036's precedent, ADR-0037).
///     <para>
///         A refusal is raised as a <see cref="TranslationRefusedException" /> in the instance's words, and a translator
///         that failed to answer as an <see cref="InstanceFailedException" /> with what the instance said of it: both are
///         <see cref="WoolyException" />s, which is what the TUI's enquiry says and the CLI reports.
///     </para>
/// </summary>
public sealed class PostTranslation(IHttpClientFactory httpClientFactory) : IPostTranslation
{
    /// <inheritdoc />
    public async Task<Translation> Translate(
        ActiveProfile profile,
        string postId,
        string? target,
        CancellationToken cancellationToken)
    {
        // The target goes as the instance's lang only when one is known: unsaid, the instance translates into the
        // account's web-interface language, which no API exposes and so nothing here could send in its place.
        using var response = await RawMastodonCall.Sent(
            httpClientFactory,
            profile,
            HttpMethod.Post,
            $"api/v1/statuses/{Uri.EscapeDataString(postId)}/translate",
            target is null ? null : new FormUrlEncodedContent([new("lang", target)]),
            cancellationToken);

        await InstanceError.EnsureAnswered(profile, response, TranslationRefusedException.For, cancellationToken);

        var answered = await response.Content.ReadFromJsonAsync<TranslationWire>(cancellationToken)
                       ?? throw new System.Text.Json.JsonException("The instance answered a translation with nothing.");

        return answered.ToTranslation();
    }

    /// <inheritdoc />
    public async Task<TranslationLanguages> Languages(ActiveProfile profile, CancellationToken cancellationToken)
    {
        using var response = await RawMastodonCall.Sent(
            httpClientFactory,
            profile,
            HttpMethod.Get,
            "api/v1/instance/translation_languages",
            content: null,
            cancellationToken);

        // An instance older than 4.2 has no such endpoint: it cannot translate, which is what an empty map says.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return TranslationLanguages.None;
        }

        await InstanceError.EnsureAnswered(profile, response, TranslationRefusedException.For, cancellationToken);

        var map = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>(cancellationToken);

        return map is null or { Count: 0 }
            ? TranslationLanguages.None
            : new TranslationLanguages(map.ToDictionary(
                pair => pair.Key,
                IReadOnlyList<string> (pair) => pair.Value ?? []));
    }
}
