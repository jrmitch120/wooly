using System.Text.Json;

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
}
