using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Accounts;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;

namespace Wooly.Core.Timelines;

/// <summary>
///     Reads a timeline through Mastonet, turning what an instance answers with into posts and asking again for as
///     many pages as the caller's limit takes. Paging is <see cref="PagedReading" />'s business, not the caller's — a
///     caller asked for posts, not for a page and a cursor to carry back.
///     <para>
///         A rate limit stops the reading rather than ending it: what already arrived is worth having, and the fetch
///         carries the limit alongside it so the caller can tell a timeline cut short from one with nothing on it.
///         Nothing waits here — ADR-0006 leaves that choice to whichever front end is reading.
///     </para>
/// </summary>
public sealed class TimelineReader(IMastodonClientFactory clientFactory) : ITimelineReader
{
    /// <summary>The most posts Mastodon serves from a timeline in one call, so the most there is any point asking for.</summary>
    private const int PageSize = 40;

    /// <inheritdoc />
    public async Task<TimelineFetch> Read(
        ActiveProfile profile,
        Timeline timeline,
        int limit,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        // Once, before the first page rather than on each one: an address becomes an id by asking the instance
        // (AccountLookup), and paying for that lookup per page would spend a call to learn what the last one already
        // knew. The other four timelines need nobody looked up and pay nothing.
        var accountId = timeline.Account is null
            ? null
            : (await AccountLookup.Resolve(client, timeline.Account, profile.Instance, cancellationToken)).Id;

        var read = await PagedReading.Collect(
            limit,
            PageSize,
            options => Fetch(client, timeline, accountId, options),
            status => PostWire.ToPost(status, profile.Instance),
            status => status.Id,
            cancellationToken);

        return read.StoppedBy is null
            ? TimelineFetch.Complete(read.Items)
            : TimelineFetch.StoppedShort(read.Items, read.StoppedBy);
    }

    private static Task<MastodonList<Status>> Fetch(
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
            _ => throw new ArgumentOutOfRangeException(nameof(timeline), timeline.Scope, "Not a timeline this client reads."),
        };
}
