using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tui.Media;
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
    // Named by the notification's own id, which is not the id of the post it is about (CONTEXT.md) — and is what a
    // refresh puts the reader back on (#84).
    private readonly Picked<Notification> _notifications = new(notifications, notification => notification.Id);

    /// <inheritdoc />
    public override string Crumb => "notifications";

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
        PostKeys.Around(
            new KeyHint("j/k", "notification"),
            [new KeyHint("d", "dismiss"), new KeyHint("D", "clear all"), Refreshing],
            new KeyHint("tab", "destination"));

    /// <summary>Which notification is picked out, as an index into what is on screen.</summary>
    public int At => _notifications.At;

    /// <summary>What is waiting, newest first.</summary>
    public IReadOnlyList<Notification> Notifications => _notifications.All;

    /// <summary>
    ///     Something the shell has to say about the inbox rather than about anything in it — that it is empty, or that
    ///     a rate limit cut the read short.
    /// </summary>
    public string? Notice { get; } = notice;

    /// <summary>The notification picked out, or <see langword="null" /> where nothing is waiting.</summary>
    public Notification? PickedNotification => _notifications.Out;

    /// <inheritdoc />
    /// <remarks>
    ///     The post the picked notification is about, so that reading it, answering it and marking it mean the same
    ///     thing here as on a feed. A follow is somebody arriving rather than something they wrote, so it has none.
    /// </remarks>
    public override Post? Picked => PickedNotification?.Post;

    /// <inheritdoc />
    protected override IPicked Walking => _notifications;

    /// <inheritdoc />
    public override void Replace(Post post) => _notifications.Rewrite(
        held => held.Post?.Id == post.Id ? held with { Post = post } : held);

    /// <inheritdoc />
    /// <remarks>
    ///     A notification about a post that is no longer there is a row about nothing, so it goes with it — the
    ///     instance still has the notification, and the next read will say so, which is the honest thing to draw.
    /// </remarks>
    public override void Remove(string postId) =>
        _notifications.Remove(notification => notification.Post?.Id == postId);

    /// <summary>Takes the notifications <paramref name="ids" /> names off the screen, once the instance has cleared them.</summary>
    public void Forget(IEnumerable<string> ids)
    {
        var going = ids.ToHashSet(StringComparer.Ordinal);

        _notifications.Remove(notification => going.Contains(notification.Id));
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, width), Role.Muted));
            lines.Add(Line.Blank);
        }

        lines.AddRange(_notifications.Rows(width, Draw));

        return lines;

        // What happened, and under it the post it happened to — indented, so that the row saying who did what reads
        // as the heading of the two rather than as another message in the list.
        IReadOnlyList<Line> Draw(Notification notification, int at, int room)
        {
            var rows = new List<Line> { Happened(notification, room, now) };

            if (notification.Post is { } post)
            {
                rows.AddRange(PostLines
                              .Feed(post, Math.Max(1, room - 2), ReadingOf(post, at), now, pictures)
                              .Select(line => line.After(new Span("  ", Role.Body))));
            }

            return rows;
        }
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
