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

    /// <summary>Every read it was asked for, in order — where a test proves what a command went looking for.</summary>
    public List<Call> Reads { get; } = [];

    /// <summary>Every notification it was asked to dismiss, in order.</summary>
    public List<Dismissed> Dismissals { get; } = [];

    /// <summary>Every profile it was asked to empty the inbox of, in order.</summary>
    public List<string> Clearances { get; } = [];

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

    public Task<Fetch<Notification>> Read(ActiveProfile profile, int limit, CancellationToken cancellationToken)
    {
        Reads.Add(new Call(profile.Name, limit));

        return Task.FromResult(_fetch);
    }

    public Task Dismiss(ActiveProfile profile, string notificationId, CancellationToken cancellationToken)
    {
        Dismissals.Add(new Dismissed(profile.Name, notificationId));

        return Task.CompletedTask;
    }

    public Task Clear(ActiveProfile profile, CancellationToken cancellationToken)
    {
        Clearances.Add(profile.Name);

        return Task.CompletedTask;
    }

    /// <summary>One read: which profile it was made as, and how many notifications it wanted.</summary>
    internal sealed record Call(string Profile, int Limit);

    /// <summary>One dismissal: which profile it was made as, and which notification it named.</summary>
    internal sealed record Dismissed(string Profile, string NotificationId);
}
