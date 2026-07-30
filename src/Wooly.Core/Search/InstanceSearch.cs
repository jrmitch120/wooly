using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Core.Search;

/// <summary>
///     Searches an instance through Mastonet, turning the three lists it answers with into accounts, hashtags and
///     posts. One call to <c>/api/v2/search</c> however narrow the query is: the instance answers with all three kinds
///     whatever is asked of it (ADR-0011), so <see cref="SearchResults.Matching" /> is what keeps only the kind wanted.
///     <para>
///         Remote things are resolved rather than skipped over. A user who pastes the address of a post or an account
///         they can see in a browser means "find me this", and an instance that has not met it yet answers with
///         nothing at all unless it is asked to go and look.
///     </para>
///     Nothing here retries and nothing here waits: a rate limit is reported rather than slept off (ADR-0006), and a
///     search is a single call, so one that is refused has nothing to hand back.
/// </summary>
public sealed class InstanceSearch(IMastodonClientFactory clientFactory) : IInstanceSearch
{
    /// <inheritdoc />
    public async Task<SearchResults> Find(
        ActiveProfile profile,
        SearchQuery query,
        CancellationToken cancellationToken)
    {
        // Mastonet's own calls take no cancellation token, so a Ctrl-C lands before the call rather than during it.
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);
        var found = await client.Search(query.Text, resolveNonLocalAccouns: true);

        // An instance that found none of a kind may leave it out of the answer altogether rather than send an empty
        // list, and "the instance said nothing about hashtags" is the same news as "it found none".
        return SearchResults.Matching(
            query.Kind,
            found.Accounts?.Select(account => AccountWire.ToAccount(account, profile.Instance)).ToList() ?? [],
            found.Hashtags?.Select(SearchWire.ToHashtag).ToList() ?? [],
            found.Statuses?.Select(status => PostWire.ToPost(status, profile.Instance)).ToList() ?? []);
    }
}
