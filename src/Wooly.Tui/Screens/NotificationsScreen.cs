using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What is waiting for this profile: who mentioned it, followed it, boosted or favorited one of its posts — and
///     anything else the instance thought worth saying, under the instance's own word for it (ADR-0010). A rail
///     destination, not a modal: it carries the unread count that was already on screen.
///     <para>
///         Arriving here reads what is waiting, which moves the read marker past it and clears the badge (ADR-0027) —
///         so the rows that were unread when the reader arrived are marked here instead, for as long as this visit
///         lasts (#397). Reading is never dismissing: they stay in the list.
///     </para>
/// </summary>
/// <remarks>
///     A notification is not the post it is about (GLOSSARY.md), and the difference is load-bearing here: <c>d</c>
///     dismisses the notification by its own id and leaves the post exactly where it was, while every other key on the
///     row acts on the post — so the two ids are kept apart rather than one standing in for the other.
/// </remarks>
/// <param name="notifications">What is waiting, newest first.</param>
/// <param name="notice">What the shell has to say about the inbox as a whole, if anything.</param>
/// <param name="unread">The ids of the notifications to draw unread for this visit; none where not said.</param>
public sealed class NotificationsScreen(
    IReadOnlyList<Notification> notifications,
    string? notice = null,
    IReadOnlySet<string>? unread = null) : Screen
{
    private readonly HashSet<string> _unread = new(unread ?? new HashSet<string>(), StringComparer.Ordinal);

    // Named by the notification's own id, which is not the id of the post it is about (GLOSSARY.md) — and is what a
    // refresh puts the reader back on (#84).

    // PROTOTYPE (#450): the groups each head stands for.
    private readonly Dictionary<string, NotificationGroupsPrototype.Group> _groups = NotificationGroupsPrototype.Build(notifications).Groups;

    private readonly Picked<Notification> _notifications = new(NotificationGroupsPrototype.Build(notifications).Heads);

    /// <inheritdoc />
    public override string Crumb => "Notifications";

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
        PostKeys.Around(
            new KeyHint("j/k", "notification"),
            [
                new KeyHint("d", "dismiss", NeedsAPick: true),
                new KeyHint("D", "clear all", NeedsAPick: true),
                Refreshing,
            ],
            PostKeys.AlongTheRail);

    /// <summary>Which notification is picked out, as an index into what is on screen.</summary>
    public int At => _notifications.At;

    /// <summary>What is waiting, newest first.</summary>
    public IReadOnlyList<Notification> Notifications => _notifications.All;

    /// <inheritdoc />
    public override IEnumerable<Mentionable> Seen => Notifications.SelectMany(Mentionable.In);

    /// <summary>
    ///     Something the shell has to say about the inbox rather than about anything in it — that it is empty, or that
    ///     a rate limit cut the read short.
    /// </summary>
    public string? Notice => $"PROTOTYPE #450 · {NotificationGroupsPrototype.Variants[NotificationGroupsPrototype.Variant]} · F2 next" + (notice is null ? "" : $"  {notice}");

    /// <summary>
    ///     Whether <paramref name="notification" /> was newer than the read marker when the reader arrived, and so is
    ///     drawn unread for this visit.
    /// </summary>
    public bool IsUnread(Notification notification) => _unread.Contains(notification.Id);

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
    /// <remarks>
    ///     A notification about a post that is no longer there is a row about nothing, so it goes with it — the
    ///     instance still has the notification, and the next read will say so, which is the honest thing to draw.
    /// </remarks>
    public override bool Heard(Change change)
    {
        switch (change)
        {
            case Change.PostChanged(var post):
                _notifications.Rewrite(held => held.Post?.Id == post.Id ? held with { Post = post } : held);

                break;

            case Change.PostGone(var id):
                _notifications.Remove(notification => notification.Post?.Id == id);

                break;

            case Change.NotificationsGone(var ids):
                TakeOff(ids);

                break;

            case Change.AllNotificationsGone:
                _notifications.Remove(_ => true);

                break;
        }

        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The unread ones not already on screen, drawn unread for the rest of this visit — the shell moves the read
    ///     marker past them as it would have on arriving, so this screen is the only thing left saying they are new.
    /// </remarks>
    public override int PutOnTop(InboxList found)
    {
        if (found is not InboxList.Notifications(var unread))
        {
            return 0;
        }

        var put = _notifications.TakeIn(unread, notification => notification.Id);

        _unread.UnionWith(put.Select(notification => notification.Id));

        return put.Count;
    }

    /// <summary>What this screen's own keys do: <c>d</c> dismisses the picked notification, and <c>D</c> all of them.</summary>
    public override Task Answer(Verb verb, Reach reach)
    {
        // PROTOTYPE (#450): nothing is dismissed on the live account from here.
        reach.Say("Prototype: nothing is dismissed.", isError: false);

        return Task.CompletedTask;
    }

    /// <summary>Clears the picked notification, which is named by its own id and not by the post's (GLOSSARY.md).</summary>
    private Task Dismiss(Reach reach)
    {
        if (PickedNotification is not { } picked)
        {
            return Task.CompletedTask;
        }

        return reach.Put(
            ask => ask.Of(token => reach.Ports.Notifications.Dismiss(reach.Profile, picked.Id, token)),
            // Whether or not the reader is still here: the notification was dismissed on the instance, and what that
            // makes stale is a fact about the instance rather than about where anybody is standing (#233, #234).
            eitherWay: () => reach.Tell(new Change.NotificationsGone([picked.Id])));
    }

    /// <summary>
    ///     Asks before emptying the inbox. Unlike dismissing one, this takes away a list nobody has necessarily read
    ///     yet and nothing brings it back — so it is asked on the same terms <c>notification clear</c> asks it.
    /// </summary>
    private void AskToClear(Reach reach)
    {
        if (Notifications.Count == 0)
        {
            return;
        }

        reach.Confirm(new Confirmation("Clear every notification?", () => Clear(reach), Going: "clear"));
    }

    /// <summary>Empties the inbox, once it has been said twice.</summary>
    private Task Clear(Reach reach) =>
        reach.Put(
            ask => ask.Of(token => reach.Ports.Notifications.Clear(reach.Profile, token)),
            eitherWay: () =>
            {
                reach.Tell(new Change.AllNotificationsGone());
                reach.Say("Cleared.", isError: false);
            });

    /// <summary>Takes the notifications <paramref name="ids" /> names off the screen, once the instance has cleared them.</summary>
    private void TakeOff(IEnumerable<string> ids)
    {
        var going = ids.ToHashSet(StringComparer.Ordinal);

        _notifications.Remove(notification => going.Contains(notification.Id));
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, drawing.Width), Role.Muted));
            lines.Add(Line.Blank);
        }

        lines.AddRange(_notifications.Rows(drawing.Width, Draw));

        return lines;

        // What happened, and under it the post it happened to — indented, so that the row saying who did what reads
        // as the heading of the two rather than as another message in the list.
        // The drawing goes down whole. Before #148 this site named four arguments and passed three, so a reader who
        // had asked for hide_drawn_caption got it honoured everywhere but here — which is the failure #148 is about
        // rather than a preference this screen was exempt from (#71).
        IReadOnlyList<Line> Draw(Notification notification, int at, int room) =>
            NotificationGroupsPrototype.Draw(
                _groups.GetValueOrDefault(notification.Id) ?? NotificationGroupsPrototype.Alone(notification),
                IsUnread(notification),
                room,
                drawing,
                post => [
                    .. PostLines
                       .Feed(post, drawing.In(Math.Max(1, room - 2)), ReadingOf(post, at))
                       .Select(line => line.After(new Span("  ", Role.Body))),
                ]);
    }

    /// <summary>
    ///     What happened, in one row: who did it, what they did, and how long ago. What they did is asked of the kind
    ///     itself, so that a mention is described here in the same words the CLI's report describes it in — and a kind
    ///     this client has never heard of is drawn under the instance's own word rather than dropped from a list whose
    ///     whole job is to say what is waiting (ADR-0010). One that was unread when the reader arrived says so before
    ///     its age, in <see cref="Role.RailUnread" />, the role of the badge that counted it.
    /// </summary>
    private static Line Happened(Notification notification, bool unread, int width, DateTimeOffset now)
    {
        var age = Elapsed.Since(notification.ReceivedAt, now);

        // The word, as an unread conversation carries it, and for the same reason (ConversationLines.With) — and so
        // that it reads with no colour (ADR-0014).
        var mark = unread ? $"{ConversationLines.UnreadMark} " : string.Empty;
        var did = $" {notification.Kind.Does}";
        var room = Math.Max(0, width - Glyphs.Columns(age) - Glyphs.Columns(mark) - Glyphs.Columns(did) - 2);

        var name = new Span(TextWrap.Clip(notification.Author, room), Role.BylineName);
        var used = name.Width + Glyphs.Columns(did) + Glyphs.Columns(mark);

        return Line.Of([
            name,
            new Span(did, Role.Muted),
            new Span(new string(' ', Math.Max(1, width - used - Glyphs.Columns(age))), Role.Muted),
            .. unread ? [new Span(mark, Role.RailUnread)] : Array.Empty<Span>(),
            new Span(age, Role.Muted),
        ]);
    }
}
