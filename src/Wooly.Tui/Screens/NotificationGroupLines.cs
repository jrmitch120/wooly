using System.Text;
using Wooly.Core.Notifications;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A <b>Notification group</b>'s heading as rows of spans: layout A7b of #450 (ADR-0032). A group about the
///     reader's own post, or with no post, is headed by its names line — the actors, then the kind's icon and sentence
///     in the kind's colour, then the unread mark and the age at the right. A post drawn whole by its actor is headed
///     by the kind alone, since the byline under it already says who and when.
/// </summary>
/// <remarks>
///     The icon carries the kind where there is no colour, as a post's marks carry theirs (ADR-0014): <c>★</c> a
///     favorite, <c>↺</c> a boost, <c>@</c> a mention, <c>+</c> a follow, and <c>•</c> any kind this client has no word
///     for, each in its kind's role (<see cref="KindLook" />).
/// </remarks>
public static class NotificationGroupLines
{
    /// <summary>How many actors a names line names before it counts the rest.</summary>
    private const int Named = 2;

    /// <summary>
    ///     What the names and the kind's sentence on a names line stand for (<see cref="Span.Item" />): the group's
    ///     actors, which a click there opens (#456). Below nought, so it cannot be taken for a face of the strip, each
    ///     of which stands for its slot (<see cref="AvatarStrip" />).
    /// </summary>
    public const int NamesPart = -1;

    /// <summary>
    ///     Whether the group's post is drawn whole by the one actor who wrote it — a mention, or a post from somebody the
    ///     reader asked to be told about — and so headed by the kind alone (#450). Decided by who wrote the post rather
    ///     than by the kind, so a kind this client has no word for is drawn by the same rule.
    /// </summary>
    public static bool ByItsActor(NotificationGroup group) =>
        group is { Post: { IsMine: false } post, Actors: [var actor, ..] } && post.Account == actor.Address;

    /// <summary>
    ///     Who did it, the way a reader would say it: one name; two joined by "and"; two and a count of everyone else,
    ///     by the instance's count rather than by how many actors it sent.
    /// </summary>
    /// <returns>
    ///     What is written, and the custom emoji written in it: each actor's own, found in that actor's name alone
    ///     from the list its account sends, since one account's <c>:verified:</c> is not another's (#524).
    /// </returns>
    private static (string Text, IReadOnlyList<Shortcode> Shortcodes) Who(NotificationGroup group)
    {
        var named = group.Actors.Take(Named).ToList();
        var others = Math.Max(0, group.Count - named.Count);

        if (named.Count == 0)
        {
            return (group.Count == 1 ? "Somebody" : $"{group.Count} accounts", []);
        }

        var text = new StringBuilder();
        var shortcodes = new List<Shortcode>();

        for (var at = 0; at < named.Count; at++)
        {
            text.Append(at == 0 ? string.Empty : others == 0 ? " and " : ", ");

            // Where this actor's name starts in the whole line, which is where its shortcodes are moved along to.
            var starts = text.Length;

            shortcodes.AddRange(Shortcode.In(named[at].Author, named[at].CustomEmoji)
                                         .Select(shortcode => shortcode with { At = shortcode.At + starts }));
            text.Append(named[at].Author);
        }

        text.Append(others switch
        {
            0 => string.Empty,
            1 => " and 1 other",
            _ => $" and {others} others",
        });

        return (text.ToString(), shortcodes);
    }

    /// <summary>
    ///     The names line: who, what in the kind's colour, and — at the right — the unread mark and the age of the
    ///     group's newest notification. The names give way first where the row is narrow. The who and the what stand
    ///     for the group's actors (<see cref="NamesPart" />); the space, the mark and the age for nothing.
    /// </summary>
    /// <remarks>
    ///     The names' custom emoji drawn as each actor's byline draws them, and the row measured as drawn, so the
    ///     sentence follows the last name and the age keeps its column whatever the names hold (#524).
    /// </remarks>
    public static Line Names(NotificationGroup group, bool unread, Drawing drawing)
    {
        var width = drawing.Width;
        var age = Elapsed.Since(group.NewestAt, drawing.Now);

        // The word, as an unread conversation carries it, and for the same reason (ConversationLines.With) — and so
        // that it reads with no colour (ADR-0014).
        var mark = unread ? $"{ConversationLines.UnreadMark} " : string.Empty;
        var look = KindLook.Of(group.Kind);
        var did = $" {look.Icon} {group.Kind.Does}";
        var room = Math.Max(0, width - Glyphs.Columns(age) - Glyphs.Columns(mark) - Glyphs.Columns(did) - 2);

        var (who, shortcodes) = Who(group);
        var name = CustomEmojiBox.Name(who, shortcodes, room, Role.BylineName, drawing);
        var used = name.Width + Glyphs.Columns(did) + Glyphs.Columns(mark);

        return name.Respanned([
            .. name.Spans.Select(span => span with { Item = NamesPart }),
            new Span(did, look.Role) { Item = NamesPart },
            new Span(new string(' ', Math.Max(1, width - used - Glyphs.Columns(age))), Role.Muted),
            .. unread ? [new Span(mark, Role.RailUnread)] : Array.Empty<Span>(),
            new Span(age, Role.Muted),
        ]);
    }

    /// <summary>
    ///     The kind alone, over a post drawn whole by its actor: no name, no age — the byline under it says both, with
    ///     the actor's face, and is left exactly as on every feed (ADR-0010, ADR-0032). The unread mark at the right.
    /// </summary>
    public static Line KindAlone(NotificationGroup group, bool unread, int width)
    {
        var look = KindLook.Of(group.Kind);
        var said = new Span(TextWrap.Clip($"{look.Icon} {group.Kind.Does}", Math.Max(1, width - 1)), look.Role);
        var mark = unread ? ConversationLines.UnreadMark : string.Empty;

        return Line.Of([
            said,
            new Span(new string(' ', Math.Max(1, width - said.Width - Glyphs.Columns(mark))), Role.Muted),
            .. unread ? [new Span(mark, Role.RailUnread)] : Array.Empty<Span>(),
        ]);
    }
}
