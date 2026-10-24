using Wooly.Core.Accounts;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What is waiting for this profile, as <b>Notification groups</b>: who mentioned it, followed it, boosted or
///     favorited one of its posts — eight favorites of one post one row, as the instance groups them and never as this
///     client would (ADR-0032) — and anything else the instance thought worth saying, under its own word for it
///     (ADR-0010). A rail destination, not a modal: it carries the unread count that was already on screen.
///     <para>
///         Arriving here reads what is waiting, which moves the read marker past it and clears the badge (ADR-0027) —
///         so the groups that were unread when the reader arrived are marked here instead, for as long as this visit
///         lasts (#397). Reading is never dismissing: they stay in the list.
///     </para>
/// </summary>
/// <remarks>
///     A notification is not the post it is about (GLOSSARY.md), and the difference is load-bearing here: <c>d</c>
///     dismisses the notification and leaves the post exactly where it was, and <c>a</c> opens its actor rather than
///     the post's author (#451), while every other key on the row acts on the post — so the two are kept apart rather
///     than one standing in for the other.
/// </remarks>
/// <param name="groups">What is waiting, newest first.</param>
/// <param name="notice">What the shell has to say about the inbox as a whole, if anything.</param>
/// <param name="unread">The keys of the groups to draw unread for this visit; none where not said.</param>
public sealed class NotificationsScreen(
    IReadOnlyList<NotificationGroup> groups,
    string? notice = null,
    IReadOnlySet<string>? unread = null) : Screen
{
    private readonly HashSet<string> _unread = new(unread ?? new HashSet<string>(), StringComparer.Ordinal);

    // Named by the group's own key, which is not the id of the post it is about (GLOSSARY.md) — and is what a refresh
    // puts the reader back on (#84).
    private readonly Picked<NotificationGroup> _groups = new(groups);

    // The face last clicked in a group's avatar strip, its slot in the group's actors: what the Verb.OpenFace a click
    // answers with opens once the shell has picked the group out. A verb carries no more than its name.
    private int _face;

    /// <inheritdoc />
    public override string Crumb => "Notifications";

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
        PostKeys.Around(
            new KeyHint("j/k", "notification"),
            [
                new KeyHint("a", "actor", NeedsAPick: true),
                new KeyHint("d", "dismiss", NeedsAPick: true),
                new KeyHint("D", "clear all", NeedsAPick: true),
                Refreshing,
            ],
            PostKeys.AlongTheRail);

    /// <summary>Which group is picked out, as an index into what is on screen.</summary>
    public int At => _groups.At;

    /// <summary>What is waiting, newest first.</summary>
    public IReadOnlyList<NotificationGroup> Groups => _groups.All;

    /// <inheritdoc />
    public override IEnumerable<Mentionable> Seen => Groups.SelectMany(Mentionable.In);

    /// <summary>
    ///     Something the shell has to say about the inbox rather than about anything in it — that it is empty, or that
    ///     a rate limit cut the read short.
    /// </summary>
    public string? Notice { get; } = notice;

    /// <summary>
    ///     Whether anything in <paramref name="group" /> was newer than the read marker when the reader arrived, or
    ///     arrived since, and so it is drawn unread for this visit.
    /// </summary>
    public bool IsUnread(NotificationGroup group) => _unread.Contains(group.Key);

    /// <summary>The group picked out, or <see langword="null" /> where nothing is waiting.</summary>
    public NotificationGroup? PickedGroup => _groups.Out;

    /// <inheritdoc />
    /// <remarks>
    ///     The post the picked group is about, so that reading it, answering it and marking it mean the same thing here
    ///     as on a feed. A follow is somebody arriving rather than something they wrote, so it has none.
    /// </remarks>
    public override Post? Picked => PickedGroup?.Post;

    /// <inheritdoc />
    protected override IPicked Walking => _groups;

    /// <inheritdoc />
    /// <remarks>
    ///     Only a post drawn whole: the reader's own is a preview with none of its references on screen, and the arrows
    ///     walk what can be seen. <c>⏎</c> opens it whole, references and all (#452).
    /// </remarks>
    protected override Post? Referencing => Picked is { IsMine: false } post ? post : null;

    /// <inheritdoc />
    /// <remarks>
    ///     A group about a post that is no longer there is a row about nothing, so it goes with it — the instance still
    ///     has the notifications, and the next read will say so, which is the honest thing to draw.
    /// </remarks>
    public override bool Heard(Change change)
    {
        switch (change)
        {
            case Change.PostChanged(var post):
                _groups.Rewrite(held => held.Post?.Id == post.Id ? held with { Post = post } : held);

                break;

            case Change.PostGone(var id):
                _groups.Remove(group => group.Post?.Id == id);

                break;

            case Change.NotificationGroupGone(var key):
                _groups.Remove(group => group.Key == key);

                break;

            case Change.AllNotificationsGone:
                _groups.Remove(_ => true);

                break;
        }

        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The groups something arrived in — those whose newest notification is newer than every group on screen — put
    ///     on top as the instance groups them now, and drawn unread for the rest of this visit: the shell moves the read
    ///     marker past them as it would have on arriving, so this screen is the only thing left saying they are new. A
    ///     group already on screen is joined rather than drawn twice — its count, actors and age are the re-read's —
    ///     and moves up, the picked one too, with the pick going along: the list reads newest first as a fresh read
    ///     would, and the pick follows the group it was on (#458). Anything not newer than the newest group is already
    ///     on screen as it was, or was dismissed.
    ///     <para>
    ///         Where the re-read read no groups — it was begun before this screen was in front — nothing is put on
    ///         top: an unread notification made into a group of its own here would carry a key an instance that groups
    ///         does not have, for <c>d</c> to dismiss nothing by. The shell reads the count again, groups and all
    ///         (<see cref="InboxBadges" />). An instance too old to group has its groups of one read as groups.
    ///     </para>
    /// </remarks>
    public override int PutOnTop(InboxList found)
    {
        if (found is not InboxList.Notifications { Groups: { } groups })
        {
            return 0;
        }

        var newest = Groups.Count > 0 ? Groups[0].NewestId : null;
        var arrived = groups
                      .Where(group => newest is null || NotificationIds.Newer(group.NewestId, newest))
                      .ToList();

        var put = _groups.TakeIn(
            arrived,
            group => group.Key,
            changed: (was, now) => !string.Equals(was.NewestId, now.NewestId, StringComparison.Ordinal),
            pickRises: true);

        _unread.UnionWith(put.Select(group => group.Key));

        return put.Count;
    }

    /// <summary>
    ///     What this screen's own keys do: <c>a</c> opens the picked group's actor, <c>d</c> dismisses it, and <c>D</c>
    ///     all of them.
    /// </summary>
    public override Task Answer(Verb verb, Reach reach)
    {
        switch (verb)
        {
            case Verb.OpenActor:
                return OpenActor(reach);

            case Verb.OpenFace:
                return OpenFace(reach);

            case Verb.Dismiss:
                return Dismiss(reach);

            case Verb.ClearAll:
                AskToClear(reach);

                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     A click on a group means more than picking it out in two places (#456): on a face in its avatar strip it opens
    ///     that one actor's account, and on its names line it opens the group's actors, as <c>a</c> does. Anywhere else
    ///     — the gaps between faces, the <c>+N</c>, the age, the post — it picks the group and nothing more, and a
    ///     double click there is <c>⏎</c>, which opens the post.
    /// </summary>
    public override Verb Clicked(int? item, int? part, bool chorded)
    {
        switch (item, part)
        {
            case (not null, NotificationGroupLines.NamesPart):
                return Verb.OpenActor;

            case (not null, >= 0 and var face):
                _face = face;

                return Verb.OpenFace;

            default:
                return Verb.None;
        }
    }

    /// <summary>
    ///     Opens whoever did what the picked group reports (GLOSSARY.md's <b>Actor</b>) — not the author of the post it
    ///     is about, who on a favorite or a boost is the reader, and who a follow does not have (#451).
    /// </summary>
    private Task OpenActor(Reach reach) =>
        PickedGroup is { } picked ? OpenActors(picked, reach) : Task.CompletedTask;

    /// <summary>
    ///     Opens the actors of <paramref name="group" />: on a group of one, that one account; on a group of several,
    ///     the screen listing every one of them, read from the group's own accounts (#455). What <c>a</c> does on the
    ///     picked group, and what a click on a group's names line does too (#456).
    /// </summary>
    public static Task OpenActors(NotificationGroup group, Reach reach) => group switch
    {
        { Count: > 1 } => reach.Open(new Subject.Actors(group)),
        { Actors: [var actor, ..] } => reach.Open(
            new Subject.Account(AccountAddress.Parse(actor.Address), WithReplies: false)),
        _ => Task.CompletedTask,
    };

    /// <summary>Opens the account of the actor whose face was clicked in the picked group's strip.</summary>
    private Task OpenFace(Reach reach) =>
        PickedGroup is { } picked && _face < picked.Actors.Count
            ? reach.Open(new Subject.Account(AccountAddress.Parse(picked.Actors[_face].Address), WithReplies: false))
            : Task.CompletedTask;

    /// <summary>
    ///     Clears the picked group whole, every notification in it, in one request by the group's key — never by the
    ///     post's id (GLOSSARY.md), and never one request per notification (#457).
    /// </summary>
    private Task Dismiss(Reach reach)
    {
        if (PickedGroup is not { } picked)
        {
            return Task.CompletedTask;
        }

        return reach.Put(
            ask => ask.Of(token => reach.Ports.Notifications.DismissGroup(reach.Profile, picked, token)),
            // Whether or not the reader is still here: the group was dismissed on the instance, and what that makes
            // stale is a fact about the instance rather than about where anybody is standing (#233, #234).
            eitherWay: () => reach.Tell(new Change.NotificationGroupGone(picked.Key)));
    }

    /// <summary>
    ///     Asks before emptying the inbox. Unlike dismissing one, this takes away a list nobody has necessarily read
    ///     yet and nothing brings it back — so it is asked on the same terms <c>notification clear</c> asks it.
    /// </summary>
    private void AskToClear(Reach reach)
    {
        if (Groups.Count == 0)
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

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, drawing.Width), Role.Muted));
            lines.Add(Line.Blank);
        }

        lines.AddRange(_groups.Rows(drawing.Width, Draw));

        return lines;

        // What happened, and under it the post it happened to — indented, so that the row saying who did what reads
        // as the heading of the two rather than as another message in the list.
        // The drawing goes down whole. Before #148 this site named four arguments and passed three, so a reader who
        // had asked for hide_drawn_caption got it honoured everywhere but here — which is the failure #148 is about
        // rather than a preference this screen was exempt from (#71).
        IReadOnlyList<Line> Draw(NotificationGroup group, int at, int room)
        {
            var unread = IsUnread(group);
            var inner = Math.Max(1, room - 2);
            var rows = new List<Line>();

            if (NotificationGroupLines.ByItsActor(group))
            {
                // No strip over a post drawn whole by its actor: its byline already has the face (#450).
                rows.Add(NotificationGroupLines.KindAlone(group, unread, room));
            }
            else
            {
                // The faces stand indented under the names line, as the post does under them (#454).
                rows.Add(NotificationGroupLines.Names(group, unread, room, drawing.Now));
                rows.AddRange(AvatarStrip.Of(group, drawing, inner).Select(line => line.After(new Span("  ", Role.Body))));
            }

            if (group.Post is { } post)
            {
                // The reader's own post is a reminder of which one it was, and anyone else's is what the notification
                // is for — told apart by who wrote it rather than by the kind, so a kind this client has no word for
                // is drawn by the same rule (#452).
                var drawn = post.IsMine
                    ? PostLines.Preview(post, inner)
                    : PostLines.Feed(post, drawing.In(inner), ReadingOf(post, at));

                rows.AddRange(drawn.Select(line => line.After(new Span("  ", Role.Body))));
            }

            return rows;
        }
    }
}
