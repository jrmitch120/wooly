using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What is waiting for this profile: who mentioned it, followed it, boosted or favorited one of its posts — and
///     anything else the instance thought worth saying, under the instance's own word for it (ADR-0010). A rail
///     destination, not a modal: it carries the unread count that was already on screen.
/// </summary>
/// <remarks>
///     A notification is not the post it is about (CONTEXT.md), and the difference is load-bearing here: <c>d</c>
///     dismisses the notification by its own id and leaves the post exactly where it was, while every other key on the
///     row acts on the post — so the two ids are kept apart rather than one standing in for the other.
/// </remarks>
public sealed class NotificationsScreen(IReadOnlyList<Notification> notifications, string? notice = null) : Screen
{
    /// <summary>The warnings the reader has asked past, so that a post mentioned twice is revealed once.</summary>
    private readonly Revealed _revealed = new();

    private readonly List<Notification> _notifications = [.. notifications];

    /// <inheritdoc />
    public override string Crumb => "notifications";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
        PostKeys.Around(
            new KeyHint("j/k", "notification"),
            [new KeyHint("d", "dismiss"), new KeyHint("D", "clear all")],
            new KeyHint("tab", "destination"));

    /// <summary>Which notification is picked out, as an index into what is on screen.</summary>
    public int At { get; private set; }

    /// <summary>What is waiting, newest first.</summary>
    public IReadOnlyList<Notification> Notifications => _notifications;

    /// <summary>
    ///     Something the shell has to say about the inbox rather than about anything in it — that it is empty, or that
    ///     a rate limit cut the read short.
    /// </summary>
    public string? Notice { get; } = notice;

    /// <summary>The notification picked out, or <see langword="null" /> where nothing is waiting.</summary>
    public Notification? PickedNotification => _notifications.Count == 0 ? null : _notifications[At];

    /// <inheritdoc />
    /// <remarks>
    ///     The post the picked notification is about, so that reading it, answering it and marking it mean the same
    ///     thing here as on a feed. A follow is somebody arriving rather than something they wrote, so it has none.
    /// </remarks>
    public override Post? Picked => PickedNotification?.Post;

    /// <inheritdoc />
    public override void Move(int by)
    {
        if (_notifications.Count > 0)
        {
            At = PickedPosts.Clamped(At, by, _notifications.Count - 1);
        }
    }

    /// <inheritdoc />
    public override bool Reveal() => Picked is { } picked && _revealed.Ask(picked);

    /// <inheritdoc />
    public override void Replace(Post post)
    {
        for (var at = 0; at < _notifications.Count; at++)
        {
            if (_notifications[at].Post?.Id == post.Id)
            {
                _notifications[at] = _notifications[at] with { Post = post };
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     A notification about a post that is no longer there is a row about nothing, so it goes with it — the
    ///     instance still has the notification, and the next read will say so, which is the honest thing to draw.
    /// </remarks>
    public override void Remove(string postId) => Forget(
        _notifications.Where(notification => notification.Post?.Id == postId).Select(notification => notification.Id));

    /// <summary>Takes the notifications <paramref name="ids" /> names off the screen, once the instance has cleared them.</summary>
    public void Forget(IEnumerable<string> ids)
    {
        var going = ids.ToHashSet(StringComparer.Ordinal);

        _notifications.RemoveAll(notification => going.Contains(notification.Id));

        At = _notifications.Count == 0 ? 0 : Math.Clamp(At, 0, _notifications.Count - 1);
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now)
    {
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, width), Role.Muted));
            lines.Add(Line.Blank);
        }

        var room = Math.Max(1, width - 1);

        for (var at = 0; at < _notifications.Count; at++)
        {
            var notification = _notifications[at];

            lines.Add(Happened(notification, room, now).After(PickedPosts.Gutter(at == At)));

            if (notification.Post is { } post)
            {
                foreach (var line in PostLines.Feed(post, Math.Max(1, room - 2), _revealed.Has(post), now))
                {
                    lines.Add(line.After(PickedPosts.Gutter(at == At), new Span("  ", Role.Body)));
                }
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    /// <summary>
    ///     What happened, in one row: who did it, what they did, and how long ago. What they did is asked of the kind
    ///     itself, so that a mention is described here in the same words the CLI's report describes it in — and a kind
    ///     this client has never heard of is drawn under the instance's own word rather than dropped from a list whose
    ///     whole job is to say what is waiting (ADR-0010).
    /// </summary>
    private static Line Happened(Notification notification, int width, DateTimeOffset now)
    {
        var age = Elapsed.Since(notification.ReceivedAt, now);
        var did = $" {notification.Kind.Does}";
        var room = Math.Max(0, width - age.Length - did.Length - 2);

        var name = TextWrap.Clip(notification.Author, room);
        var used = name.Length + did.Length;

        return Line.Of([
            new Span(name, Role.BylineName),
            new Span(did, Role.Muted),
            new Span(new string(' ', Math.Max(1, width - used - age.Length)), Role.Muted),
            new Span(age, Role.Muted),
        ]);
    }
}
