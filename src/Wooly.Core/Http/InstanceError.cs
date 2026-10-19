using System.Text.Json;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Core.Http;

/// <summary>What an instance said was wrong, out of the body it refused a request with.</summary>
internal static class InstanceError
{
    /// <summary>
    ///     Mastodon's <c>error</c> out of <paramref name="response" />'s body, or <see langword="null" /> where it said
    ///     nothing readable.
    /// </summary>
    public static async Task<string?> Reason(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Nothing, where <paramref name="response" /> is a success; otherwise what it was, as something a front end
    ///     can say — the one shape every hand-rolled write that reports a refusal shares (#372, #404).
    /// </summary>
    /// <param name="refused">The refusal to raise, out of what the instance said was wrong.</param>
    /// <exception cref="InstanceFailedException">
    ///     The instance failed to answer — a <c>5xx</c> — which says nothing of what was sent, and may pass.
    /// </exception>
    public static async Task EnsureAnswered(
        ActiveProfile profile,
        HttpResponseMessage response,
        Func<string?, WoolyException> refused,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new InstanceFailedException(profile.Instance, response.StatusCode);
        }

        throw refused(await Reason(response, cancellationToken).ConfigureAwait(false));
    }
}
