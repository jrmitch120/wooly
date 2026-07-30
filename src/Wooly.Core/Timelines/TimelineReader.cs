using Mastonet;
using Mastonet.Entities;
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
    /// <inheritdoc />
    public async Task<TimelineFetch> Read(
        ActiveProfile profile,
        Timeline timeline,
        int limit,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        var read = await PagedReading.Collect(
            limit,
            options => Fetch(client, timeline, options),
            status => PostWire.ToPost(status, profile.Instance),
            status => status.Id,
            cancellationToken);

        return read.StoppedBy is null
            ? TimelineFetch.Complete(read.Items)
            : TimelineFetch.StoppedShort(read.Items, read.StoppedBy);
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
}
