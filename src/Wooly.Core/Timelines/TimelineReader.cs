using Mastonet;
using Mastonet.Entities;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;

namespace Wooly.Core.Timelines;

/// <summary>
///     Reads a timeline through Mastonet, turning what an instance answers with into posts and asking again for as
///     many pages as the caller's limit takes. Paging is this class's business alone — a caller asked for posts, not
///     for a page and a cursor to carry back.
///     <para>
///         A rate limit stops the reading rather than ending it: what already arrived is worth having, and the fetch
///         carries the limit alongside it so the caller can tell a timeline cut short from one with nothing on it.
///         Nothing waits here — ADR-0006 leaves that choice to whichever front end is reading.
///     </para>
/// </summary>
public sealed class TimelineReader(IMastodonClientFactory clientFactory) : ITimelineReader
{
    /// <summary>The most posts Mastodon serves in one call, so the most there is any point asking for.</summary>
    private const int PageSize = 40;

    /// <inheritdoc />
    public async Task<TimelineFetch> Read(
        ActiveProfile profile,
        Timeline timeline,
        int limit,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);
        var posts = new List<Post>();
        string? nextPage = null;

        while (posts.Count < limit)
        {
            // Mastonet's timeline calls take no cancellation token of their own, so a Ctrl-C lands between pages
            // rather than during one. Between pages is where the pages this class added are, which is the part a
            // caller could not have stopped itself.
            cancellationToken.ThrowIfCancellationRequested();

            var wanted = Math.Min(PageSize, limit - posts.Count);
            MastodonList<Status> page;

            try
            {
                page = await Fetch(client, timeline, new ArrayOptions { Limit = wanted, MaxId = nextPage });
            }
            catch (RateLimitedException rateLimit)
            {
                return TimelineFetch.StoppedShort(posts, rateLimit);
            }

            posts.AddRange(page.Select(status => ToPost(status, profile.Instance)));

            // Nothing came back, so asking again cannot do better however much the instance says is left.
            if (page.Count == 0)
            {
                break;
            }

            // The instance names where the next page starts in a link header, and that is the authority on whether
            // there is one: a page can come back short of what was asked for and still not be the last, because an
            // instance drops filtered posts from a page after counting them. Only where it names no next page does a
            // page with room to spare mean the end — and there the oldest post just read is where a further page
            // would start, since a page comes back newest first.
            nextPage = page.NextPageMaxId ?? (page.Count < wanted ? null : page[^1].Id);

            if (nextPage is null)
            {
                break;
            }
        }

        return TimelineFetch.Complete(posts);
    }

    private static Task<MastodonList<Status>> Fetch(IMastodonClient client, Timeline timeline, ArrayOptions options) =>
        timeline.Scope switch
        {
            TimelineScope.Home => client.GetHomeTimeline(options),
            TimelineScope.Local => client.GetPublicTimeline(options, local: true),
            TimelineScope.Federated => client.GetPublicTimeline(options),
            TimelineScope.Tag => client.GetTagTimeline(timeline.Hashtag!, options),
            _ => throw new ArgumentOutOfRangeException(nameof(timeline), timeline.Scope, "Not a timeline this client reads."),
        };

    private static Post ToPost(Status status, string instance) => new()
    {
        Id = status.Id,
        Account = Qualify(status.Account, instance),
        Author = string.IsNullOrWhiteSpace(status.Account.DisplayName)
            ? status.Account.UserName
            : status.Account.DisplayName,
        PostedAt = AsUtc(status.CreatedAt),
        Content = PostContent.ToPlainText(status.Content),

        // The wire says "no warning" with an empty string, which is not the same thing as a warning to print.
        ContentWarning = string.IsNullOrWhiteSpace(status.SpoilerText) ? null : status.SpoilerText,
        Boosts = status.ReblogCount,
        Favorites = status.FavouritesCount,
        Replies = status.RepliesCount,
        Boosted = status.Reblog is null ? null : ToPost(status.Reblog, instance),
        Url = status.Url,
    };

    /// <summary>
    ///     Mastodon timestamps every post in UTC. A parser that hands one back as <see cref="DateTimeKind.Unspecified" />
    ///     is still handing back UTC, so it is read as such rather than as this machine's local time.
    /// </summary>
    private static DateTimeOffset AsUtc(DateTime moment) => moment.Kind switch
    {
        DateTimeKind.Unspecified => new DateTimeOffset(moment, TimeSpan.Zero),
        _ => new DateTimeOffset(moment.ToUniversalTime(), TimeSpan.Zero),
    };

    /// <summary>
    ///     An instance names its own accounts by bare username and everyone else's by <c>username@instance</c>. A
    ///     timeline mixes the two, so the bare ones are qualified here — otherwise two posts side by side would say
    ///     who wrote them in two different ways.
    /// </summary>
    private static string Qualify(Account account, string instance) =>
        account.AccountName.Contains('@') ? account.AccountName : $"{account.AccountName}@{instance}";
}
