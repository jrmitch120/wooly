using System.Globalization;
using System.Text.Json.Serialization;
using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using WireAccount = Mastonet.Entities.Account;
using WireNotification = Mastonet.Entities.Notification;
using WireStatus = Mastonet.Entities.Status;

namespace Wooly.Core.Notifications;

/// <summary>
///     What <c>/api/v2/notifications</c> answers with: the groups, and every account and post they name, sent once
///     alongside them rather than inside each — and the one crossing from that answer to this project's
///     <see cref="NotificationGroup" />, beside <see cref="NotificationWire" /> for a single notification.
///     <para>
///         Written out here because Mastonet 3.1.3 knows only <c>/api/v1</c> notifications (ADR-0032). The accounts and
///         posts are Mastonet's own entities, so that they cross into this project through the same mappings every
///         other account and post does.
///     </para>
/// </summary>
internal sealed record GroupedNotificationsWire
{
    [JsonPropertyName("accounts")]
    public IReadOnlyList<WireAccount> Accounts { get; init; } = [];

    [JsonPropertyName("statuses")]
    public IReadOnlyList<WireStatus> Statuses { get; init; } = [];

    [JsonPropertyName("notification_groups")]
    public IReadOnlyList<GroupWire> NotificationGroups { get; init; } = [];

    /// <summary>
    ///     The groups, each with its actors and post found among those sent alongside. An actor the answer named but
    ///     did not send is left out rather than invented: the count still says how many there are.
    /// </summary>
    public IReadOnlyList<NotificationGroup> ToGroups(ActiveProfile reader)
    {
        var accounts = Accounts.GroupBy(account => account.Id)
                               .ToDictionary(same => same.Key, same => same.First(), StringComparer.Ordinal);
        var statuses = Statuses.GroupBy(status => status.Id)
                               .ToDictionary(same => same.Key, same => same.First(), StringComparer.Ordinal);

        return
        [
            .. NotificationGroups.Select(group => new NotificationGroup
            {
                Key = group.GroupKey,
                Kind = NotificationWire.ToKind(group.Type),
                Count = group.NotificationsCount,
                NewestId = group.MostRecentNotificationId.ToString(CultureInfo.InvariantCulture),
                NewestAt = group.LatestPageNotificationAt.ToUniversalTime(),
                Actors =
                [
                    .. group.SampleAccountIds
                            .Where(accounts.ContainsKey)
                            .Select(id => AccountWire.ToAccount(accounts[id], reader.Instance)),
                ],
                Post = group.StatusId is { } statusId && statuses.TryGetValue(statusId, out var status)
                    ? PostWire.ToPost(status, reader)
                    : null,
            }),
        ];
    }

    /// <summary>
    ///     A single notification off <c>/api/v1</c> as a group of one — what every notification is on an instance too
    ///     old to group, under the key Mastodon gives a notification it does not group.
    /// </summary>
    public static NotificationGroup Alone(WireNotification notification, ActiveProfile reader) => new()
    {
        Key = $"ungrouped-{notification.Id}",
        Kind = NotificationWire.ToKind(notification.Type),
        Count = 1,
        NewestId = notification.Id,
        NewestAt = MastodonWire.AsUtc(notification.CreatedAt),
        Actors = [AccountWire.ToAccount(notification.Account, reader.Instance)],
        Post = notification.Status is null ? null : PostWire.ToPost(notification.Status, reader),
    };

    /// <summary>One group, as the instance describes it.</summary>
    internal sealed record GroupWire
    {
        [JsonPropertyName("group_key")]
        public string GroupKey { get; init; } = string.Empty;

        [JsonPropertyName("notifications_count")]
        public int NotificationsCount { get; init; }

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        /// <summary>
        ///     A JSON number, unlike every other id on the endpoint (#449) — read as one, and from a string too, in case
        ///     an instance comes to send it the way it sends the rest.
        /// </summary>
        [JsonPropertyName("most_recent_notification_id")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long MostRecentNotificationId { get; init; }

        [JsonPropertyName("latest_page_notification_at")]
        public DateTimeOffset LatestPageNotificationAt { get; init; }

        [JsonPropertyName("sample_account_ids")]
        public IReadOnlyList<string> SampleAccountIds { get; init; } = [];

        [JsonPropertyName("status_id")]
        public string? StatusId { get; init; }
    }
}
