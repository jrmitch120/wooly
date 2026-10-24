using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Wooly.Core.Accounts;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Paging;
using Wooly.Core.Profiles;
using WireAccount = Mastonet.Entities.Account;

namespace Wooly.Core.Notifications;

/// <summary>
///     Reads and clears notifications through Mastonet. Reading pages through <see cref="PagedReading" />, the same loop
///     a timeline reads down, so an inbox and a timeline cannot come to disagree about where a list ends.
///     <para>
///         Nothing is filtered on the way through. Mastodon offers to leave kinds out of what it sends, and this asks for
///         all of them: a kind this client has no word for is still a notification the account has, and one it never sees
///         is one it can neither read nor dismiss.
///     </para>
///     Nothing here retries and nothing here waits. A dismissal the instance answered is never sent again, because
///     ADR-0006 resends nothing an instance has already taken, and a rate limit is reported rather than slept off.
/// </summary>
/// <remarks>
///     The read marker is the one thing read around Mastonet rather than through it, by <see cref="RawMastodonCall" />
///     over the same <see cref="HttpClient" />: Mastonet 3.1.3 has the call but cannot read the answer
///     (<see cref="MarkersWire" />).
/// </remarks>
public sealed class NotificationInbox(IMastodonClientFactory clientFactory, IHttpClientFactory httpClientFactory)
    : INotificationInbox
{
    /// <summary>
    ///     The most notifications Mastodon serves in one call. Ten fewer than a timeline's page, which is why the loop
    ///     takes this rather than holding one number for everything: asking for 40 would get 30 back, and a full page
    ///     that looks short is exactly what the loop reads as the end of the list.
    /// </summary>
    private const int PageSize = 30;

    /// <inheritdoc />
    public async Task<Fetch<Notification>> Read(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        return await PagedReading.Collect(
            limit,
            PageSize,
            options => client.GetNotifications(options),
            notification => NotificationWire.ToNotification(notification, profile),
            notification => notification.Id,
            cancellationToken);
    }

    /// <summary>
    ///     How many groups to ask <c>/api/v2/notifications</c> for at once: its documented default, which every instance
    ///     serves. The read goes on by the instance's <c>next</c> link, not by whether a page came back full (#449).
    /// </summary>
    private const int GroupPageSize = 40;

    /// <summary>
    ///     The profiles whose instance has no grouped read, learned from the first read that found none — so that every
    ///     later read this session goes straight to <c>/api/v1</c> rather than asking again (ADR-0032).
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _ungrouped = new(StringComparer.Ordinal);

    /// <summary>What <see cref="_ungrouped" /> knows a profile by.</summary>
    private static string Known(ActiveProfile profile) => $"{profile.Name}@{profile.Instance}";

    /// <inheritdoc />
    /// <remarks>
    ///     Raw, because Mastonet 3.1.3 knows only <c>/api/v1</c> notifications, and sent without <c>grouped_types</c>:
    ///     which kinds group is the instance's call (ADR-0032). An instance that answers the first page with a 404 is
    ///     older than Mastodon 4.3, and is read through <c>/api/v1</c> instead, each notification a group of one.
    /// </remarks>
    public async Task<Fetch<NotificationGroup>> ReadGroups(
        ActiveProfile profile,
        int limit,
        CancellationToken cancellationToken)
    {
        var known = Known(profile);

        if (!_ungrouped.ContainsKey(known))
        {
            try
            {
                return await PagedReading.Collect(
                    limit,
                    GroupPageSize,
                    options => GroupedPage(profile, options, cancellationToken),
                    group => group,
                    idOf: null,
                    cancellationToken);
            }
            catch (NoGroupedRead)
            {
                _ungrouped[known] = true;
            }
        }

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        return await PagedReading.Collect(
            limit,
            PageSize,
            options => client.GetNotifications(options),
            notification => NotificationGroup.Alone(NotificationWire.ToNotification(notification, profile)),
            notification => notification.Id,
            cancellationToken);
    }

    /// <summary>One page of groups, and where the instance says the next one starts.</summary>
    /// <exception cref="NoGroupedRead">The instance has no grouped read at all.</exception>
    private Task<Page<NotificationGroup>> GroupedPage(
        ActiveProfile profile,
        Mastonet.ArrayOptions options,
        CancellationToken cancellationToken) =>
        RawPage<GroupedNotificationsWire, NotificationGroup>(
            profile,
            "api/v2/notifications",
            options,
            answer => answer.ToGroups(profile),
            cancellationToken,
            whenFirstPageMissing: () => new NoGroupedRead());

    /// <summary>
    ///     One page of a raw <c>GET</c> of <paramref name="path" />, a list Mastonet does not know, and where the
    ///     instance's <c>next</c> link says the next page starts — the one way on for a list paged by ids that are not
    ///     its items' own. Said once for every raw list read here.
    /// </summary>
    /// <param name="path">The list's path, without its query.</param>
    /// <param name="options">How many to ask for, and from where.</param>
    /// <param name="items">What the answer's body holds, as this project's own.</param>
    /// <param name="whenFirstPageMissing">
    ///     What a 404 on the first page is thrown as, where it says more than that the read failed; otherwise it fails
    ///     as any other answer that is not a success.
    /// </param>
    private async Task<Page<TItem>> RawPage<TWire, TItem>(
        ActiveProfile profile,
        string path,
        Mastonet.ArrayOptions options,
        Func<TWire, IReadOnlyList<TItem>> items,
        CancellationToken cancellationToken,
        Func<Exception>? whenFirstPageMissing = null)
        where TWire : new()
    {
        var query = options.MaxId is { } maxId
            ? $"{path}?limit={options.Limit}&max_id={Uri.EscapeDataString(maxId)}"
            : $"{path}?limit={options.Limit}";

        using var response = await RawMastodonCall.Sent(
            httpClientFactory,
            profile,
            HttpMethod.Get,
            query,
            content: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound && options.MaxId is null && whenFirstPageMissing is { } missing)
        {
            throw missing();
        }

        response.EnsureSuccessStatusCode();

        var answer = await response.Content.ReadFromJsonAsync<TWire>(cancellationToken) ?? new TWire();

        return new Page<TItem>(items(answer), LinkHeader.NextMaxId(response));
    }

    /// <summary>
    ///     How many of a group's actors to ask for at once: the most Mastodon serves from a list of accounts. The read
    ///     goes on by the instance's <c>next</c> link, not by whether a page came back full (#449).
    /// </summary>
    private const int ActorPageSize = 80;

    /// <inheritdoc />
    /// <remarks>
    ///     Raw, as the grouped read is: <c>GET /api/v2/notifications/:group_key/accounts</c>, which Mastonet 3.1.3 does
    ///     not know. It pages by notification ids, which are not the accounts' own, so nothing read here can stand in
    ///     for the cursor: the instance's link is the only way on, and a page with none is the last.
    /// </remarks>
    public Task<Fetch<Account>> ReadGroupActors(
        ActiveProfile profile,
        string groupKey,
        int limit,
        CancellationToken cancellationToken) =>
        PagedReading.Collect(
            limit,
            ActorPageSize,
            options => RawPage<List<WireAccount>, WireAccount>(
                profile,
                $"api/v2/notifications/{Uri.EscapeDataString(groupKey)}/accounts",
                options,
                accounts => accounts,
                cancellationToken),
            account => AccountWire.ToAccount(account, profile.Instance),
            idOf: null,
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    ///     Two questions: where the marker is, then what is above it (<see cref="ReadNewer" />).
    /// </remarks>
    public async Task<UnreadNotifications> ReadUnread(
        ActiveProfile profile,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var markers = await RawMastodonCall.Get<MarkersWire>(
            httpClientFactory,
            profile,
            "api/v1/markers",
            [new KeyValuePair<string, string>("timeline[]", "notifications")],
            cancellationToken);

        if (markers?.Notifications is not { LastReadId: { Length: > 0 } since })
        {
            return UnreadNotifications.NoMarker;
        }

        return new UnreadNotifications(since, await ReadNewer(profile, since, limit, cancellationToken));
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The id carried onto every page as <c>since_id</c>, because the instance's link to the next page names only
    ///     where that page starts — left to the link, a second page would run on past it.
    /// </remarks>
    public async Task<Fetch<Notification>> ReadNewer(
        ActiveProfile profile,
        string notificationId,
        int limit,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        return await PagedReading.Collect(
            limit,
            PageSize,
            options =>
            {
                options.SinceId = notificationId;

                return client.GetNotifications(options);
            },
            notification => NotificationWire.ToNotification(notification, profile),
            notification => notification.Id,
            cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Sent raw for the same reason the marker is read raw: Mastonet's <c>SetMarkers</c> reads its answer as the
    ///     marker it cannot read (<see cref="MarkersWire" />), so the library's call would throw on a move the instance
    ///     had made. The answer is not read here at all — whether the instance took it is all the port promises.
    /// </remarks>
    public async Task<bool> MoveMarker(
        ActiveProfile profile,
        string notificationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var response = await RawMastodonCall.Sent(
                httpClientFactory,
                profile,
                HttpMethod.Post,
                "api/v1/markers",
                new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("notifications[last_read_id]", notificationId),
                ]),
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception unsent) when (unsent is RateLimitedException or TransientNetworkException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task Dismiss(ActiveProfile profile, string notificationId, CancellationToken cancellationToken)
    {
        // Mastonet's own calls take no cancellation token, so a Ctrl-C lands before the call rather than during it.
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        await client.DismissNotification(notificationId);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Raw, as the grouped read is: Mastonet 3.1.3 has no <c>/api/v2</c> dismiss. Which call goes is decided by what
    ///     the grouped read already learned of the instance, so an instance too old to group is never asked for a group.
    /// </remarks>
    public async Task DismissGroup(
        ActiveProfile profile,
        NotificationGroup group,
        CancellationToken cancellationToken)
    {
        if (_ungrouped.ContainsKey(Known(profile)))
        {
            await Dismiss(profile, group.NewestId, cancellationToken);

            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var response = await RawMastodonCall.Sent(
            httpClientFactory,
            profile,
            HttpMethod.Post,
            $"api/v2/notifications/{Uri.EscapeDataString(group.Key)}/dismiss",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task Clear(ActiveProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = clientFactory.CreateClient(profile.Instance, profile.AccessToken);

        await client.ClearNotifications();
    }

    /// <summary>An instance answering the grouped read with a 404, which is one too old to have it.</summary>
    private sealed class NoGroupedRead : Exception;
}
