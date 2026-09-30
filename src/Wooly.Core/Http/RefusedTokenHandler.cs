using System.Net;
using System.Text.Json;
using Wooly.Core.Errors;

namespace Wooly.Core.Http;

/// <summary>
///     Turns an instance's 401 to a request made with a token into an <see cref="AuthenticationException" />: the token
///     was refused — revoked or expired since it was stored — which is the one failure fixed by signing the profile in
///     again, and so the one each front end can say how to fix (ADR-0006, #248).
///     <para>
///         Only where the request carried a token. Registering this client and trading a code for a token carry none,
///         and a 401 there is about the client or the code, which the caller has better words for.
///     </para>
/// </summary>
internal sealed class RefusedTokenHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized || request.Headers.Authorization is null)
        {
            return response;
        }

        var instance = request.RequestUri?.Host ?? "the instance";
        var reason = await Reason(response, cancellationToken).ConfigureAwait(false);

        response.Dispose();

        throw new AuthenticationException(
            reason is null ? $"{instance} refused the access token." : $"{instance} refused the access token: {reason}");
    }

    /// <summary>
    ///     What the instance said was wrong with the token — Mastodon's <c>error</c> — or <see langword="null" /> where
    ///     it said nothing readable.
    /// </summary>
    private static async Task<string?> Reason(HttpResponseMessage response, CancellationToken cancellationToken)
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
