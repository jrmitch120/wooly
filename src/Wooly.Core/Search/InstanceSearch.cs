using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Mastonet;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Core.Search;

/// <summary>
///     Searches an instance, turning the three lists it answers with into accounts, hashtags and posts. One call to
///     <c>/api/v2/search</c>, made raw over the <see cref="HttpClient" /> every Mastonet call runs through
///     (<see cref="RawMastodonCall" />), because Mastonet sends none of <c>type</c>, <c>limit</c> or <c>offset</c> and a
///     page needs all three (ADR-0011's amendment, #599). A narrowed search names its kind to the instance, and
///     <see cref="SearchResults.Matching" /> still drops the kinds not asked for, so that what was not asked for is
///     absent however the instance answered.
///     <para>
///         Remote things are resolved rather than skipped over. A user who pastes the address of a post or an account
///         they can see in a browser means "find me this", and an instance that has not met it yet answers with
///         nothing at all unless it is asked to go and look. Past the first page the instance stops resolving without
///         saying so, which costs nothing: the first page already did.
///     </para>
///     Nothing here retries and nothing here waits: a rate limit is reported rather than slept off (ADR-0006), and a
///     search is a single call, so one that is refused has nothing to hand back.
/// </summary>
/// <remarks>
///     <see cref="FindFollowed" /> stays on Mastonet: the endpoint it asks has everything it needs there.
/// </remarks>
public sealed class InstanceSearch(IMastodonClientFactory clientFactory, IHttpClientFactory httpClientFactory)
    : IInstanceSearch
{
    /// <inheritdoc />
    /// <exception cref="Errors.SearchRefusedException">The instance answered with a refusal.</exception>
    /// <exception cref="Errors.InstanceFailedException">The instance failed to answer — a <c>5xx</c>.</exception>
    public async Task<SearchResults> Find(
        ActiveProfile profile,
        SearchQuery query,
        CancellationToken cancellationToken)
    {
        using var response = await RawMastodonCall.Sent(
            httpClientFactory,
            profile,
            HttpMethod.Get,
            $"api/v2/search?{string.Join("&", Parameters(query))}",
            content: null,
            cancellationToken);

        // Mastonet said a refusal in the instance's own words, and so does this, rather than as a bare status that a
        // front end would report as a defect — and an instance that failed to answer as failing, the one way every raw
        // adapter says the two (#598's review).
        await InstanceError.EnsureAnswered(
            profile,
            response,
            reason => new SearchRefusedException(reason),
            cancellationToken);

        var found = await response.Content.ReadFromJsonAsync<SearchResultsWire>(cancellationToken)
                    ?? new SearchResultsWire();

        // An instance that found none of a kind may leave it out of the answer altogether rather than send an empty
        // list, and "the instance said nothing about hashtags" is the same news as "it found none". Every hashtag sent
        // is kept, the exact match an instance can put ahead of a full page included: it is the tag most likely meant.
        return SearchResults.Matching(
            query.Kind,
            found.Accounts?.Select(account => AccountWire.ToAccount(account, profile.Instance)).ToList() ?? [],
            found.Hashtags?.Select(HashtagWire.ToHashtag).ToList() ?? [],
            found.Statuses?.Select(status => PostWire.ToPost(status, profile)).ToList() ?? []);
    }

    /// <summary>
    ///     The query string, in the order the endpoint documents it: what to look for, resolving, and then only what
    ///     was said — so a search asked nothing more is sent exactly as it was through Mastonet.
    /// </summary>
    private static IEnumerable<string> Parameters(SearchQuery query)
    {
        yield return $"q={Uri.EscapeDataString(query.Text)}";
        yield return "resolve=true";

        if (TypeOf(query.Kind) is { } type)
        {
            yield return $"type={type}";
        }

        if (query.Page.Limit is { } limit)
        {
            yield return $"limit={limit.ToString(CultureInfo.InvariantCulture)}";
        }

        if (query.Page.Offset is { } offset)
        {
            yield return $"offset={offset.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    /// <summary>
    ///     How the instance spells <paramref name="kind" />, which is not how a user does: it calls posts
    ///     <c>statuses</c>. Nothing for every kind, which is what sending no <c>type</c> asks for.
    /// </summary>
    private static string? TypeOf(SearchKind kind) => kind switch
    {
        SearchKind.Accounts => "accounts",
        SearchKind.Hashtags => "hashtags",
        SearchKind.Posts => "statuses",
        _ => null,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>> FindFollowed(
        ActiveProfile profile,
        string query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        try
        {
            var found = await client.SearchAccounts(
                query,
                IInstanceSearch.FollowedFound,
                resolveNonLocalAccouns: false,
                onlyFollowing: true);

            return [.. found.Select(account => AccountWire.ToAccount(account, profile.Instance))];
        }
        // An instance declining to answer, however Mastonet spells it, found nobody worth suggesting — the same
        // silence #204 gave the standing a list is decorated with. A rate limit is still raised, as the port says.
        catch (Exception unanswered) when (unanswered is ServerErrorException or HttpRequestException or JsonException)
        {
            return [];
        }
    }
}
