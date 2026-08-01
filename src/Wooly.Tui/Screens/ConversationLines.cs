using Wooly.Core.Conversations;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A conversation as rows of spans, and the words for one. What <see cref="AccountLines" /> is for an account, and
///     here for the same reason: two screens draw a conversation — the list of them and the thread one opens onto —
///     and a heading that read one way on one and another way on the next would be two ideas of the same thing.
/// </summary>
public static class ConversationLines
{
    /// <summary>
    ///     What a conversation whose posts have all been taken down says. One spelling, because the list says it and
    ///     the thread says it again, and two spellings of one sentence is one of them going stale.
    /// </summary>
    public const string NothingLeft = "Nothing left in this conversation.";

    /// <summary>The mark an unread conversation carries, which is a word rather than a glyph — see <see cref="With" />.</summary>
    private const string UnreadMark = "unread";

    /// <summary>
    ///     Who the conversation is with, as the mentions that would reach them. An instance says who a conversation is
    ///     with rather than who is having it, so one with nobody in it is one whose only other account has been taken
    ///     down — said out loud rather than drawn as an empty row.
    /// </summary>
    public static string Who(Conversation conversation) =>
        conversation.With.Count == 0
            ? "nobody"
            : string.Join(", ", conversation.With.Select(account => $"@{account}"));

    /// <summary>
    ///     Who it is with, and whether anything in it is unread. The mark is the word rather than a glyph: this
    ///     client's glyphs already say who can see a post — <c>○ ◌ ● ✉</c> — and a second circle beside <c>●</c> would
    ///     be one mark too many to tell apart at a glance. It takes <see cref="Role.RailUnread" />, the same role as
    ///     the badge counting it on the rail (<c>docs/tui-shell.md</c>).
    /// </summary>
    public static Line With(Conversation conversation, int width)
    {
        var who = Who(conversation);

        if (!conversation.Unread)
        {
            return Line.Of(TextWrap.Clip(who, width), Role.BylineHandle);
        }

        var named = TextWrap.Clip(who, Math.Max(0, width - UnreadMark.Length - 1));

        return Line.Of([
            new Span(named, Role.BylineHandle),
            new Span(new string(' ', Math.Max(1, width - named.Length - UnreadMark.Length)), Role.Body),
            new Span(UnreadMark, Role.RailUnread),
        ]);
    }
}
