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
///     for. A follow and an unfamiliar kind take <see cref="Role.BylineHandle" />, the role of an account named — a
///     follow is somebody arriving, and a kind with no word of its own has no mark's colour to borrow.
/// </remarks>
public static class NotificationGroupLines
{
    /// <summary>How many actors a names line names before it counts the rest.</summary>
    private const int Named = 2;

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
    public static string Who(NotificationGroup group)
    {
        var named = group.Actors.Take(Named).Select(actor => actor.Author).ToList();
        var others = Math.Max(0, group.Count - named.Count);

        return (named.Count, others) switch
        {
            (0, _) => group.Count == 1 ? "Somebody" : $"{group.Count} accounts",
            (_, 0) => string.Join(" and ", named),
            (_, 1) => $"{string.Join(", ", named)} and 1 other",
            _ => $"{string.Join(", ", named)} and {others} others",
        };
    }

    /// <summary>
    ///     The names line: who, what in the kind's colour, and — at the right — the unread mark and the age of the
    ///     group's newest notification. The names give way first where the row is narrow.
    /// </summary>
    public static Line Names(NotificationGroup group, bool unread, int width, DateTimeOffset now)
    {
        var age = Elapsed.Since(group.NewestAt, now);

        // The word, as an unread conversation carries it, and for the same reason (ConversationLines.With) — and so
        // that it reads with no colour (ADR-0014).
        var mark = unread ? $"{ConversationLines.UnreadMark} " : string.Empty;
        var did = $" {Icon(group.Kind)} {group.Kind.Does}";
        var room = Math.Max(0, width - Glyphs.Columns(age) - Glyphs.Columns(mark) - Glyphs.Columns(did) - 2);

        var name = new Span(TextWrap.Clip(Who(group), room), Role.BylineName);
        var used = name.Width + Glyphs.Columns(did) + Glyphs.Columns(mark);

        return Line.Of([
            name,
            new Span(did, KindRole(group.Kind)),
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
        var said = new Span(
            TextWrap.Clip($"{Icon(group.Kind)} {group.Kind.Does}", Math.Max(1, width - 1)),
            KindRole(group.Kind));
        var mark = unread ? ConversationLines.UnreadMark : string.Empty;

        return Line.Of([
            said,
            new Span(new string(' ', Math.Max(1, width - said.Width - Glyphs.Columns(mark))), Role.Muted),
            .. unread ? [new Span(mark, Role.RailUnread)] : Array.Empty<Span>(),
        ]);
    }

    /// <summary>The mark that says the kind without colour.</summary>
    private static string Icon(NotificationKind kind) =>
        kind == NotificationKind.Favorite ? "★"
        : kind == NotificationKind.Boost ? "↺"
        : kind == NotificationKind.Mention ? "@"
        : kind == NotificationKind.Follow ? "+"
        : "•";

    /// <summary>The kind's colour: a post's mark's where the kind is one, and an account's handle's where it is not.</summary>
    private static Role KindRole(NotificationKind kind) =>
        kind == NotificationKind.Favorite ? Role.Favorite
        : kind == NotificationKind.Boost ? Role.Boost
        : kind == NotificationKind.Mention ? Role.Mention
        : Role.BylineHandle;
}
