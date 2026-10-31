using System.Net.Http.Json;
using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Accounts;
using Wooly.Core.Http;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Core.Timelines;

/// <summary>
///     Reads a timeline through Mastonet, turning what an instance answers with into posts and asking again for as
///     many pages as the caller's limit takes. Paging is <see cref="PagedReading" />'s business, not the caller's — a
///     caller asked for posts, not for a page and a cursor to carry back. What it can carry back is where to read on
///     from (<see cref="Onward" />), which says nothing about either (ADR-0007's amendment).
///     <para>
///         A rate limit stops the reading rather than ending it: what already arrived is worth having, and the fetch
///         carries the limit alongside it so the caller can tell a timeline cut short from one with nothing on it.
///         Nothing waits here — ADR-0006 leaves that choice to whichever front end is reading.
///     </para>
///     <para>
///         A <b>Tag set</b> is the one timeline read around Mastonet (<see cref="RawMastodonCall" />), whose tag
///         timeline takes one tag and no <c>any[]</c> (ADR-0033), over the same <see cref="HttpClient" />: four tags
///         to an ask, the asks merged newest first (<see cref="MergedReading" />). A set of one run is read as any
///         other timeline is, a rate limit included; in a set of several, one run failing fails the read, a rate
///         limit included, and nothing of the runs that answered is kept (ADR-0033's amendment of ADR-0007).
///     </para>
/// </summary>
public sealed class TimelineReader(IMastodonClientFactory clientFactory, IHttpClientFactory httpClientFactory)
    : ITimelineReader
{
    /// <summary>The most posts Mastodon serves from a timeline in one call, so the most there is any point asking for.</summary>
    private const int PageSize = 40;

    /// <inheritdoc />
    public Task<Fetch<Post>> Read(
        ActiveProfile profile,
        Timeline timeline,
        int limit,
        CancellationToken cancellationToken) =>
        Collect(profile, timeline, from: null, limit, cancellationToken);

    /// <inheritdoc />
    public Task<Fetch<Post>> ReadOn(
        ActiveProfile profile,
        Timeline timeline,
        Onward from,
        int limit,
        CancellationToken cancellationToken) =>
        Collect(profile, timeline, from, limit, cancellationToken);

    /// <summary>One read of <paramref name="timeline" />, from the top or from where an earlier one said to read on.</summary>
    private async Task<Fetch<Post>> Collect(
        ActiveProfile profile,
        Timeline timeline,
        Onward? from,
        int limit,
        CancellationToken cancellationToken)
    {
        if (timeline.Set is { } set)
        {
            // Four tags to an ask, in the order written, and the asks merged here (ADR-0033).
            var runs = set.Tags
                .Chunk(TagSet.TagsPerAsk)
                .Select(run => (Func<ArrayOptions, Task<Page<Status>>>)(options =>
                    RunPage(profile, run, options, cancellationToken)))
                .ToList();

            // A set of four or fewer is one run, and so one ask: one list paged as Home is, which keeps what arrived
            // when a rate limit stops it part way. It has no middle for a missing run to leave a hole in.
            if (runs is [var only])
            {
                return await PagedReading.Collect(
                    limit,
                    PageSize,
                    only,
                    status => PostWire.ToPost(status, profile),
                    status => status.Id,
                    cancellationToken,
                    from: from);
            }

            return await MergedReading.Collect(
                limit,
                PageSize,
                runs,
                status => PostWire.ToPost(status, profile),
                post => post.Id,
                cancellationToken,
                from: from);
        }

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        // Once, before the first page rather than on each one: an address becomes an id by asking the instance
        // (AccountLookup), and paying for that lookup per page would spend a call to learn what the last one already
        // knew. The other four timelines need nobody looked up and pay nothing — and neither does an account that
        // arrived already resolved, which AccountLookup.IdOf is what decides. The CLI has no resolved account to
        // give, so it goes on paying for the lookup exactly as it did.
        var accountId = timeline.Account is null
            ? null
            : await AccountLookup.IdOf(client, timeline.Account, profile.Instance, cancellationToken);

        return await PagedReading.Collect(
            limit,
            PageSize,
            options => Page(client, timeline, accountId, options),
            status => PostWire.ToPost(status, profile),
            status => status.Id,
            cancellationToken,
            from: from);
    }

    /// <summary>
    ///     One page of one run of a set's tags in one ask: its first tag in the path, and the rest as <c>any[]</c>,
    ///     which the instance unions with it (ADR-0033). A run holds no more tags than one ask reads
    ///     (<see cref="TagSet.TagsPerAsk" />), so none is dropped. Every tag is one word, which is what makes the first
    ///     safe to put in a path.
    /// </summary>
    private async Task<Page<Status>> RunPage(
        ActiveProfile profile,
        IReadOnlyList<string> run,
        ArrayOptions options,
        CancellationToken cancellationToken)
    {
        List<string> query = [$"limit={options.Limit}"];

        if (options.MaxId is { } maxId)
        {
            query.Add($"max_id={Uri.EscapeDataString(maxId)}");
        }

        // The brackets unescaped, as RawMastodonCall sends id[]: an escaped name is a parameter Mastodon does not
        // answer to.
        query.AddRange(run.Skip(1).Select(tag => $"any[]={Uri.EscapeDataString(tag)}"));

        using var response = await RawMastodonCall.Sent(
            httpClientFactory,
            profile,
            HttpMethod.Get,
            $"api/v1/timelines/tag/{Uri.EscapeDataString(run[0])}?{string.Join("&", query)}",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var posts = await response.Content.ReadFromJsonAsync<List<Status>>(cancellationToken) ?? [];

        return new Page<Status>(posts, LinkHeader.NextMaxId(response));
    }

    private static Task<MastodonList<Status>> Page(
        IMastodonClient client,
        Timeline timeline,
        string? accountId,
        ArrayOptions options) =>
        timeline.Scope switch
        {
            TimelineScope.Home => client.GetHomeTimeline(options),
            TimelineScope.Local => client.GetPublicTimeline(options, local: true),
            TimelineScope.Federated => client.GetPublicTimeline(options),
            TimelineScope.Tag => client.GetTagTimeline(timeline.Hashtag!, options),

            // Read raw, above, before a client is made: never here.
            TimelineScope.TagSet => throw new InvalidOperationException("A tag set is read around Mastonet."),

            // Boosts left in and replies left out, which is what an account's own page shows on the web and what
            // somebody who pressed 'a' on a post is asking to see: what this account says and passes on, rather than
            // half of a hundred conversations they answered.
            TimelineScope.Account => client.GetAccountStatuses(
                accountId!,
                options,
                onlyMedia: false,
                excludeReplies: true,
                pinned: false,
                excludeReblogs: false),

            // The same endpoint with both of those flipped. Replies come back because a pin is often one, and leaving
            // them out above is exactly what puts a pinned reply out of reach. One page, no link header, so the
            // collecting loop stops of its own accord — ADR-0007's one paging path, unchanged. It never reaches that
            // loop's max_id cursor either, which would be wrong for a run in the account's pin order rather than
            // newest first: an instance caps pins far below a page, so the run always arrives short and stops there.
            TimelineScope.Pinned => client.GetAccountStatuses(
                accountId!,
                options,
                onlyMedia: false,
                excludeReplies: false,
                pinned: true,
                excludeReblogs: false),

            // The account's timeline again with only the replies let back in: boosts stay, nothing is pinned, and it
            // pages newest first exactly as the account's posts do (#211).
            TimelineScope.WithReplies => client.GetAccountStatuses(
                accountId!,
                options,
                onlyMedia: false,
                excludeReplies: false,
                pinned: false,
                excludeReblogs: false),
            _ => throw new ArgumentOutOfRangeException(nameof(timeline), timeline.Scope, "Not a timeline this client reads."),
        };
}
