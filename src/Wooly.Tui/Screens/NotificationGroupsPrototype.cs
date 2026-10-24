// PROTOTYPE (#450) — throwaway, never merged. Answers: what shape should a notification group's avatar strip take?
//
// The real Notifications screen, with the real notifications grouped on screen by kind and post (a stand-in for the
// instance's grouping, #453), and five made-up groups on top built from the real actors so the avatars are real
// faces: one actor, two, three follows, eight, forty. F2 cycles the layouts:
//   A  4×2 strip on its own row under the names line
//   A1 A with air: two columns between faces, a blank row above and below the strip
//   A2 A with the strip above the sentence, as Mastodon's web app has it
//   A3 A with the kind's icon (★ ↺ + @) in the gutter beside the strip
//   A4 A1 + A2 + A3 together
//   B  3×1 strip on its own row under the names line
//   C  3×1 avatars inline, in front of the names
//   D  4×2 avatars in a band beside the names line and the kind
//   E  no avatars (the baseline)
// A post drawn whole (a mention) gets no strip in any layout: its byline already has the actor's face.
// Your own posts are drawn as a two-line muted preview (#452); anyone else's in full.
// d and D do nothing here, so nothing on the live account is dismissed.

using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

internal static class NotificationGroupsPrototype
{
    public static readonly string[] Variants =
    [
        "A · 4×2 strip on its own row",
        "A1 · A with air: two columns between faces, a blank row above and below",
        "A2 · A with the strip above the sentence, as the web app has it",
        "A3 · A with the kind's icon in the gutter beside the strip",
        "A4 · A1 + A2 + A3 together",
        "B · 3×1 strip on its own row",
        "C · 3×1 inline before the names",
        "D · 4×2 band beside the names",
        "E · no avatars",
    ];

    public static int Variant { get; private set; }

    public static void Next() => Variant = (Variant + 1) % Variants.Length;

    public sealed record Actor(string Name, string Account, string? Avatar);

    public sealed record Group(
        NotificationKind Kind,
        IReadOnlyList<Actor> Actors,
        int Count,
        DateTimeOffset At,
        Post? Post,
        string HeadId);

    private const int Sampled = 8;

    /// <summary>The groups to show, newest first, and the notification standing in for each so picking works.</summary>
    public static (IReadOnlyList<Notification> Heads, Dictionary<string, Group> Groups) Build(
        IReadOnlyList<Notification> notifications)
    {
        var groups = new List<Group>();

        var pool = notifications
                   .GroupBy(notification => notification.Account)
                   .Select(by => by.First())
                   .Select(notification => new Actor(notification.Author, notification.Account, notification.AvatarUrl))
                   .ToList();

        if (pool.Count == 0)
        {
            pool.Add(new Actor("Nobody", "nobody@example.social", null));
        }

        Actor[] Take(int count) => [.. Enumerable.Range(0, count).Select(at => pool[at % pool.Count])];

        var ownPost = notifications.FirstOrDefault(n => n.Post is { IsMine: true })?.Post;
        var now = DateTimeOffset.UtcNow;

        if (ownPost is not null)
        {
            groups.Add(new Group(NotificationKind.Favorite, Take(1), 1, now.AddMinutes(-2), ownPost, "proto-1"));
            groups.Add(new Group(NotificationKind.Boost, Take(2), 2, now.AddMinutes(-9), ownPost, "proto-2"));
        }

        groups.Add(new Group(NotificationKind.Follow, Take(3), 3, now.AddMinutes(-30), null, "proto-3"));

        if (ownPost is not null)
        {
            groups.Add(new Group(NotificationKind.Favorite, Take(Sampled), 8, now.AddHours(-2), ownPost, "proto-8"));
            groups.Add(new Group(NotificationKind.Favorite, Take(Sampled), 40, now.AddHours(-5), ownPost, "proto-40"));
        }

        // The real ones, grouped the way the instance would: favorites, boosts and follows of one post together.
        var grouping = new Dictionary<string, List<Notification>>();
        var order = new List<string>();

        foreach (var notification in notifications)
        {
            var groups4 = notification.Kind == NotificationKind.Favorite
                          || notification.Kind == NotificationKind.Boost
                          || notification.Kind == NotificationKind.Follow;
            var key = groups4 ? $"{notification.Kind.Name}:{notification.Post?.Id}" : $"one:{notification.Id}";

            if (!grouping.TryGetValue(key, out var members))
            {
                grouping[key] = members = [];
                order.Add(key);
            }

            members.Add(notification);
        }

        foreach (var key in order)
        {
            var members = grouping[key];
            var newest = members[0];

            groups.Add(new Group(
                newest.Kind,
                [.. members.Take(Sampled).Select(n => new Actor(n.Author, n.Account, n.AvatarUrl))],
                members.Count,
                newest.ReceivedAt,
                newest.Post,
                newest.Id));
        }

        var heads = groups.Select(group => notifications.FirstOrDefault(n => n.Id == group.HeadId) ?? new Notification
        {
            Id = group.HeadId,
            Kind = group.Kind,
            ReceivedAt = group.At,
            Account = group.Actors[0].Account,
            Author = group.Actors[0].Name,
            Post = group.Post,
            AvatarUrl = group.Actors[0].Avatar,
        }).ToList();

        return (heads, groups.ToDictionary(group => group.HeadId));
    }

    public static Group Alone(Notification notification) => new(
        notification.Kind,
        [new Actor(notification.Author, notification.Account, notification.AvatarUrl)],
        1,
        notification.ReceivedAt,
        notification.Post,
        notification.Id);

    /// <summary>One group's rows in the current variant.</summary>
    public static IReadOnlyList<Line> Draw(
        Group group,
        bool unread,
        int room,
        Drawing drawing,
        Func<Post, IReadOnlyList<Line>> fullPost)
    {
        var rows = new List<Line>();

        // A post drawn whole already carries its author's face in its byline; a strip of the same face above it is
        // the face twice, so a mention (or any post shown whole, by its actor) gets no strip.
        var variant = group.Post is { IsMine: false } whole && whole.Account == group.Actors[0].Account ? Variants.Length - 1 : Variant;

        switch (variant)
        {
            case 0:
                rows.Add(Names(group, unread, room, drawing.Now, lead: []));
                rows.AddRange(Strip(group, columns: 4, high: 2, room, drawing, indent: 2));

                break;

            case 1:
            {
                rows.Add(Names(group, unread, room, drawing.Now, lead: []));
                var strip = Strip(group, columns: 4, high: 2, room, drawing, indent: 2, gap: 2, plusRow: 1);

                if (strip.Count > 0)
                {
                    rows.Add(Line.Blank);
                    rows.AddRange(strip);
                    rows.Add(Line.Blank);
                }

                break;
            }

            case 2:
                rows.AddRange(Strip(group, columns: 4, high: 2, room, drawing, indent: 2));
                rows.Add(Names(group, unread, room, drawing.Now, lead: []));

                break;

            case 3:
            {
                var strip = Strip(group, columns: 4, high: 2, room, drawing, indent: 3, icon: Icon(group));

                rows.Add(Names(group, unread, room, drawing.Now, lead: []));
                rows.AddRange(strip);

                break;
            }

            case 4:
            {
                var strip = Strip(group, columns: 4, high: 2, room, drawing, indent: 3, gap: 2, plusRow: 1, icon: Icon(group));

                if (strip.Count > 0)
                {
                    rows.Add(Line.Blank);
                    rows.AddRange(strip);
                    rows.Add(Line.Blank);
                }

                rows.Add(Names(group, unread, room, drawing.Now, lead: []));

                break;
            }

            case 5:
                rows.Add(Names(group, unread, room, drawing.Now, lead: []));
                rows.AddRange(Strip(group, columns: 3, high: 1, room, drawing, indent: 2));

                break;

            case 6:
            {
                var strip = Strip(group, columns: 3, high: 1, Math.Min(room, 3 + (5 * 4) + 4), drawing, indent: 0, gapAfter: 1);

                if (strip.Count == 0)
                {
                    rows.Add(Names(group, unread, room, drawing.Now, lead: []));
                }
                else
                {
                    var lead = strip[0];
                    var named = Names(group, unread, room - lead.Spans.Sum(span => span.Width), drawing.Now, lead: []);

                    rows.Add(named.After([.. lead.Spans]) with
                    {
                        Insets = lead.Insets,
                        AlsoWants = lead.AlsoWants,
                    });
                }

                break;
            }

            case 7:
            {
                var strip = Strip(group, columns: 4, high: 2, Math.Min(room, (3 * 5) + 5), drawing, indent: 0, gapAfter: 1);

                if (strip.Count == 0)
                {
                    rows.Add(Names(group, unread, room, drawing.Now, lead: []));
                }
                else
                {
                    var used = strip[0].Spans.Sum(span => span.Width);
                    var named = NamesOnly(group, unread, room - used, drawing.Now);
                    var kind = Line.Of(new Span(TextWrap.Clip(Sentence(group), room - used), Role.Muted));

                    rows.Add(named.After([.. strip[0].Spans]) with
                    {
                        Insets = strip[0].Insets,
                        AlsoWants = strip[0].AlsoWants,
                    });
                    rows.Add(kind.After([.. strip[1].Spans]));
                }

                break;
            }

            default:
                rows.Add(Names(group, unread, room, drawing.Now, lead: []));

                break;
        }

        if (group.Post is { } post)
        {
            rows.AddRange(post.IsMine ? Preview(post, room - 2) : fullPost(post));
        }

        return rows;
    }

    private static string Who(Group group)
    {
        var first = group.Actors[0].Name;

        return group.Count switch
        {
            1 => first,
            2 => $"{first} and {group.Actors[Math.Min(1, group.Actors.Count - 1)].Name}",
            3 => $"{first}, {group.Actors[1].Name} and 1 other",
            _ => $"{first}, {group.Actors[1].Name} and {group.Count - 2} others",
        };
    }

    private static string Sentence(Group group) => group.Kind.Does;

    private static Span Icon(Group group) =>
        new(
            group.Kind == NotificationKind.Favorite ? "★"
            : group.Kind == NotificationKind.Boost ? "↺"
            : group.Kind == NotificationKind.Follow ? "+"
            : group.Kind == NotificationKind.Mention ? "@"
            : "•",
            Role.Muted);

    private static Line Names(Group group, bool unread, int width, DateTimeOffset now, Span[] lead)
    {
        var age = Elapsed.Since(group.At, now);
        var mark = unread ? $"{ConversationLines.UnreadMark} " : string.Empty;
        var did = $" {Sentence(group)}";
        var room = Math.Max(0, width - Glyphs.Columns(age) - Glyphs.Columns(mark) - Glyphs.Columns(did) - 2);
        var name = new Span(TextWrap.Clip(Who(group), room), Role.BylineName);
        var used = name.Width + Glyphs.Columns(did) + Glyphs.Columns(mark);

        return Line.Of([
            .. lead,
            name,
            new Span(did, Role.Muted),
            new Span(new string(' ', Math.Max(1, width - used - Glyphs.Columns(age))), Role.Muted),
            .. unread ? [new Span(mark, Role.RailUnread)] : Array.Empty<Span>(),
            new Span(age, Role.Muted),
        ]);
    }

    private static Line NamesOnly(Group group, bool unread, int width, DateTimeOffset now)
    {
        var age = Elapsed.Since(group.At, now);
        var mark = unread ? $"{ConversationLines.UnreadMark} " : string.Empty;
        var room = Math.Max(0, width - Glyphs.Columns(age) - Glyphs.Columns(mark) - 1);
        var name = new Span(TextWrap.Clip(Who(group), room), Role.BylineName);
        var used = name.Width + Glyphs.Columns(mark);

        return Line.Of([
            name,
            new Span(new string(' ', Math.Max(1, width - used - Glyphs.Columns(age))), Role.Muted),
            .. unread ? [new Span(mark, Role.RailUnread)] : Array.Empty<Span>(),
            new Span(age, Role.Muted),
        ]);
    }

    /// <summary>
    ///     A strip of avatars <paramref name="high" /> rows tall, each <paramref name="columns" /> wide with a column of
    ///     air after it, as many as fit <paramref name="width" />, then a muted +N. No rows where nothing is drawn.
    /// </summary>
    private static IReadOnlyList<Line> Strip(
        Group group,
        int columns,
        int high,
        int width,
        Drawing drawing,
        int indent,
        int gapAfter = 0,
        int gap = 1,
        int plusRow = 0,
        Span? icon = null)
    {
        var rows = Enumerable.Range(0, high).Select(_ => new List<Span>()).ToArray();
        var insets = new List<Inset>();
        var wants = new List<Drawn>();

        foreach (var row in rows)
        {
            row.Add(new Span(new string(' ', indent), Role.Body));
        }

        if (icon is { } shown0)
        {
            rows[0][0] = new Span($" {shown0.Text}".PadRight(indent), shown0.Role);
        }

        var column = indent;
        var plusRoom = 5;
        var fits = Math.Max(1, (width - indent - plusRoom) / (columns + gap));
        var shown = group.Actors.Take(Math.Min(fits, Sampled)).ToList();

        foreach (var actor in shown)
        {
            var box = PictureBox.Fixed(
                actor.Avatar is null ? null : Drawn.Avatar(actor.Account, actor.Avatar),
                columns,
                high,
                drawing.Pictures,
                drawing.Raster);

            if (box.Wanted is null)
            {
                // Nothing will be drawn on this terminal: no strip at all.
                if (drawing.Raster is null || !Drawing.Draws(drawing.Pictures, drawing.Raster, out _))
                {
                    return [];
                }

                continue;
            }

            wants.Add(box.Wanted);

            for (var at = 0; at < high; at++)
            {
                rows[at].Add(box.Cells(at));
                rows[at].Add(new Span(new string(' ', gap), Role.Body));
                insets.AddRange(box.Insets(at, column).Select(inset => inset with { Rows = high }));
            }

            column += columns + gap;
        }

        if (wants.Count == 0)
        {
            return [];
        }

        var rest = group.Count - wants.Count;

        if (rest > 0)
        {
            for (var at = 0; at < high; at++)
            {
                rows[at].Add(at == Math.Min(plusRow, high - 1)
                    ? new Span($"+{rest}", Role.Muted)
                    : new Span(new string(' ', Glyphs.Columns($"+{rest}")), Role.Body));
            }
        }

        foreach (var row in rows)
        {
            row.Add(new Span(new string(' ', gapAfter), Role.Body));
        }

        return
        [
            .. rows.Select((spans, at) => at == 0
                ? new Line(spans) { Insets = insets, AlsoWants = wants }
                : new Line(spans)),
        ];
    }

    private static IReadOnlyList<Line> Preview(Post post, int width)
    {
        var text = post.ContentWarning is { Length: > 0 } warning ? $"⚠ {warning}" : post.Content;
        var wrapped = TextWrap.Wrap(text.ReplaceLineEndings(" "), Math.Max(1, width));
        var shown = wrapped.Take(2).ToList();

        if (wrapped.Count > 2)
        {
            shown[1] = TextWrap.Clip(shown[1], Math.Max(1, width - 1)) + "…";
        }

        return [.. shown.Select(row => Line.Of(new Span("  ", Role.Body), new Span(row, Role.Muted)))];
    }
}
