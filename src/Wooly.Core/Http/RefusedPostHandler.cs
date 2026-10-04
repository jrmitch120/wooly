using System.Net;
using System.Text.RegularExpressions;
using Wooly.Core.Errors;

namespace Wooly.Core.Http;

/// <summary>
///     Turns an instance's 422 to a post being published or changed into a <see cref="PostRefusedException" />: the
///     instance has judged the post as written and turned it down — longer than it allows, most often — which the
///     writer can do something about, and has to be told in the instance's own words (#319).
///     <para>
///         Read here, off the status, because Mastonet turns every refusal into one exception that carries no status
///         at all, and a 404 to an edit (the post has gone) or a 403 is not the post being judged. Only the two calls
///         that send a post's words: <c>POST /api/v1/statuses</c> and <c>PUT /api/v1/statuses/:id</c>. A 422 to a
///         boost, a vote or an upload is about that, and is handed back for its own caller to say.
///     </para>
/// </summary>
internal sealed partial class RefusedPostHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.UnprocessableEntity || !SendsAPost(request))
        {
            return response;
        }

        var reason = await InstanceError.Reason(response, cancellationToken).ConfigureAwait(false);

        response.Dispose();

        throw new PostRefusedException(reason);
    }

    private static bool SendsAPost(HttpRequestMessage request) =>
        request.RequestUri?.AbsolutePath is { } path
        && ((request.Method == HttpMethod.Post && path == "/api/v1/statuses")
            || (request.Method == HttpMethod.Put && AStatus().IsMatch(path)));

    [GeneratedRegex("^/api/v1/statuses/[^/]+$")]
    private static partial Regex AStatus();
}
