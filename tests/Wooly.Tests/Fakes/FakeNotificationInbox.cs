using Wooly.Core.Errors;
using Wooly.Core.Notifications;
using Wooly.Core.Paging;
using Wooly.Core.Profiles;

namespace Wooly.Tests.Fakes;

/// <summary>
///     An account's notifications without the instance. ADR-0005's primary seam for anything above the API layer: a
///     command test says what was waiting and then asks what was read, dismissed or cleared, and never fakes HTTP to
///     do it.
/// </summary>
internal sealed class FakeNotificationInbox(Fetch<Notification> fetch) : INotificationInbox
{
    private Fetch<Notification> _fetch = fetch;

    /// <summary>The access token every call was made with, in order — where a test proves who it was made as.</summary>
    public List<string> Tokens { get; } = [];

    /// <summary>Every read it was asked for, in order — where a test proves what a command went looking for.</summary>
    public List<Call> Reads { get; } = [];

    /// <summary>Every notification it was asked to dismiss, in order.</summary>
    public List<Dismissed> Dismissals { get; } = [];

    /// <summary>Every profile it was asked to empty the inbox of, in order.</summary>
    public List<string> Clearances { get; } = [];

    /// <summary>
    ///     What each profile's inbox is answered with, by the profile's name, where a test tells two profiles' inboxes
    ///     apart — or finishes one by hand (#243). Everything not named here is answered as the inbox holds.
    /// </summary>
    public Dictionary<string, Func<Task<Fetch<Notification>>>> ByProfile { get; } = [];

    /// <summary>An inbox holding <paramref name="notifications" />, read to the end of whatever was asked for.</summary>
    public static FakeNotificationInbox Holding(params Notification[] notifications) =>
        new(Fetch<Notification>.Complete(notifications));

    /// <summary>An instance whose rate limit stopped the read with <paramref name="notifications" /> already in hand.</summary>
    public static FakeNotificationInbox RateLimitedAfter(params Notification[] notifications) =>
        new(Fetch<Notification>.StoppedShort(
            notifications,
            new RateLimitedException("mastodon.social", new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero))));

    /// <summary>
    ///     What the inbox holds from here on: what arrived while the reader was reading it, which is what a refresh is
    ///     asked to notice (#84).
    /// </summary>
    public void NowHolding(params Notification[] notifications) =>
        _fetch = Fetch<Notification>.Complete(notifications);

    /// <summary>
    ///     Where set, what a read of the list waits for before it answers — and only that read, so that a test can hold
    ///     an arrival in flight while a count is read past it. It answers with what the inbox held when it was asked,
    ///     as an instance would: what arrives during the wait is not on it.
    /// </summary>
    public TaskCompletionSource? ReadsWaitFor { get; set; }

    public async Task<Fetch<Notification>> Read(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        Reads.Add(new Call(profile.Name, limit));
        Tokens.Add(profile.AccessToken);

        var held = _fetch;

        if (ReadsWaitFor is { } waiting)
        {
            await waiting.Task;
        }

        return ByProfile.TryGetValue(profile.Name, out var answer) ? await answer() : held;
    }

    /// <summary>
    ///     The groups it holds, where a test says them: from here on the grouped read answers with these. Where not
    ///     said, what it holds is read as an instance too old to group would answer — each notification a group of one —
    ///     so that a test about single notifications reads the same on the grouped screen.
    /// </summary>
    public IReadOnlyList<NotificationGroup>? Groups { get; set; }

    /// <summary>Every grouped read it was asked for, in order.</summary>
    public List<Call> GroupReads { get; } = [];

    public async Task<Fetch<NotificationGroup>> ReadGroups(
        ActiveProfile profile,
        int limit,
        CancellationToken cancellationToken)
    {
        GroupReads.Add(new Call(profile.Name, limit));
        Tokens.Add(profile.AccessToken);

        if (Groups is { } groups)
        {
            return Fetch<NotificationGroup>.Complete([.. groups.Take(limit)]);
        }

        var held = _fetch;

        if (ReadsWaitFor is { } waiting)
        {
            await waiting.Task;
        }

        var read = ByProfile.TryGetValue(profile.Name, out var answer) ? await answer() : held;
        var alone = read.Items.Take(limit).Select(ANotification.Alone).ToList();

        return read.StoppedBy is { } stoppedBy
            ? Fetch<NotificationGroup>.StoppedShort(alone, stoppedBy)
            : Fetch<NotificationGroup>.Complete(alone);
    }

    /// <summary>Each group's actors, by the group's key, as its accounts endpoint lists them; none where not said.</summary>
    public Dictionary<string, IReadOnlyList<Wooly.Core.Accounts.Account>> ActorsOf { get; } = new(StringComparer.Ordinal);

    /// <summary>Every read of a group's actors it was asked for, in order.</summary>
    public List<ActorRead> ActorReads { get; } = [];

    /// <summary>Up to <paramref name="limit" /> of the actors <see cref="ActorsOf" /> holds for the group.</summary>
    public Task<Fetch<Wooly.Core.Accounts.Account>> ReadGroupActors(
        ActiveProfile profile,
        string groupKey,
        int limit,
        CancellationToken cancellationToken)
    {
        ActorReads.Add(new ActorRead(profile.Name, groupKey, limit));
        Tokens.Add(profile.AccessToken);

        var actors = ActorsOf.GetValueOrDefault(groupKey) ?? [];

        return Task.FromResult(Fetch<Wooly.Core.Accounts.Account>.Complete([.. actors.Take(limit)]));
    }

    /// <summary>
    ///     The account's read marker: the id of the newest notification it has read, or <see langword="null" /> for an
    ///     account that has never had one. Nothing read by default — <c>"0"</c>, below every id a test hands it — so
    ///     what the inbox holds is all unread unless a test says otherwise.
    /// </summary>
    public string? Marker { get; set; } = "0";

    /// <summary>Every read of what is unread it was asked for, in order.</summary>
    public List<Call> UnreadReads { get; } = [];

    /// <summary>
    ///     What it holds above <see cref="Marker" />, up to <paramref name="limit" /> — compared as numbers, the way an
    ///     instance's ids order. A read cut short by a rate limit is cut short here too.
    /// </summary>
    public async Task<UnreadNotifications> ReadUnread(
        ActiveProfile profile,
        int limit,
        CancellationToken cancellationToken)
    {
        UnreadReads.Add(new Call(profile.Name, limit));
        Tokens.Add(profile.AccessToken);

        if (Marker is not { } marker)
        {
            return UnreadNotifications.NoMarker;
        }

        var held = ByProfile.TryGetValue(profile.Name, out var answer) ? await answer() : _fetch;
        var since = held.Items.Where(notification => Newer(notification.Id, marker)).Take(limit).ToList();

        return new UnreadNotifications(
            marker,
            held.StoppedBy is { } stoppedBy
                ? Fetch<Notification>.StoppedShort(since, stoppedBy)
                : Fetch<Notification>.Complete(since));
    }

    /// <summary>
    ///     What it holds newer than <paramref name="notificationId" />, up to <paramref name="limit" /> — recorded among
    ///     <see cref="UnreadReads" />, being the same read from an id the caller names.
    /// </summary>
    public async Task<Fetch<Notification>> ReadNewer(
        ActiveProfile profile,
        string notificationId,
        int limit,
        CancellationToken cancellationToken)
    {
        UnreadReads.Add(new Call(profile.Name, limit));
        Tokens.Add(profile.AccessToken);

        var held = ByProfile.TryGetValue(profile.Name, out var answer) ? await answer() : _fetch;
        var newer = held.Items.Where(notification => Newer(notification.Id, notificationId)).Take(limit).ToList();

        return held.StoppedBy is { } stoppedBy
            ? Fetch<Notification>.StoppedShort(newer, stoppedBy)
            : Fetch<Notification>.Complete(newer);
    }

    /// <summary>Every move of the marker it was asked for, in order — whether or not it took it.</summary>
    public List<Dismissed> MarkerMoves { get; } = [];

    /// <summary>Whether it turns every move of the marker down, as an instance that refused or was not reached would.</summary>
    public bool TurnsMarkerMovesDown { get; set; }

    /// <summary>Moves <see cref="Marker" /> to <paramref name="notificationId" />, unless it turns moves down.</summary>
    public Task<bool> MoveMarker(ActiveProfile profile, string notificationId, CancellationToken cancellationToken)
    {
        MarkerMoves.Add(new Dismissed(profile.Name, notificationId));
        Tokens.Add(profile.AccessToken);

        if (TurnsMarkerMovesDown)
        {
            return Task.FromResult(false);
        }

        Marker = notificationId;

        return Task.FromResult(true);
    }

    /// <summary>
    ///     Where set, what a dismissal waits for before it answers — so that a test can hold one in flight while a
    ///     count is read past it.
    /// </summary>
    public TaskCompletionSource? DismissalsWaitFor { get; set; }

    public async Task Dismiss(ActiveProfile profile, string notificationId, CancellationToken cancellationToken)
    {
        Dismissals.Add(new Dismissed(profile.Name, notificationId));
        Tokens.Add(profile.AccessToken);

        if (DismissalsWaitFor is { } waiting)
        {
            await waiting.Task;
        }

        // Gone from the instance, so a later read — of the list or of what is unread — no longer finds it.
        _fetch = _fetch with { Items = [.. _fetch.Items.Where(notification => notification.Id != notificationId)] };
    }

    /// <summary>Every group it was asked to dismiss, in order, by the group's key.</summary>
    public List<GroupDismissed> GroupDismissals { get; } = [];

    /// <summary>
    ///     Which held notifications each group in <see cref="Groups" /> is made of, by the group's key, where a test
    ///     says — so that dismissing a group takes them off what a later read of what is unread finds, as the instance
    ///     would. A group of one read from what it holds is made of its own notification without being said.
    /// </summary>
    public Dictionary<string, IReadOnlyList<string>> Members { get; } = [];

    public async Task DismissGroup(
        ActiveProfile profile,
        NotificationGroup group,
        CancellationToken cancellationToken)
    {
        GroupDismissals.Add(new GroupDismissed(profile.Name, group.Key));
        Tokens.Add(profile.AccessToken);

        if (DismissalsWaitFor is { } waiting)
        {
            await waiting.Task;
        }

        // Gone from the instance, every notification in it, so a later read — of the groups, of the list or of what
        // is unread — no longer finds any of them.
        var going = Members.TryGetValue(group.Key, out var members)
            ? members.ToHashSet(StringComparer.Ordinal)
            : [group.NewestId];

        Groups = Groups?.Where(held => held.Key != group.Key).ToList();
        _fetch = _fetch with { Items = [.. _fetch.Items.Where(notification => !going.Contains(notification.Id))] };
    }

    public Task Clear(ActiveProfile profile, CancellationToken cancellationToken)
    {
        Clearances.Add(profile.Name);
        Tokens.Add(profile.AccessToken);

        return Task.CompletedTask;
    }

    /// <summary>Whether <paramref name="id" /> is newer than <paramref name="than" />: longer, or later at the same length.</summary>
    private static bool Newer(string id, string than) =>
        id.Length != than.Length ? id.Length > than.Length : string.CompareOrdinal(id, than) > 0;

    /// <summary>One read: which profile it was made as, and how many notifications it wanted.</summary>
    internal sealed record Call(string Profile, int Limit);

    /// <summary>One read of a group's actors: which profile, which group by its key, and how many were wanted.</summary>
    internal sealed record ActorRead(string Profile, string GroupKey, int Limit);

    /// <summary>One dismissal: which profile it was made as, and which notification it named.</summary>
    internal sealed record Dismissed(string Profile, string NotificationId);

    /// <summary>One group dismissal: which profile it was made as, and which group's key it named.</summary>
    internal sealed record GroupDismissed(string Profile, string Key);
}
