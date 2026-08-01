using Wooly.Core.Conversations;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The conversations this profile is in: who each is with, whether anything in it is unread, and the last thing
///     said. A rail destination, carrying the count of the ones still unread.
/// </summary>
/// <remarks>
///     A row is a conversation, not a post — so this screen answers to none of the keys that act on one. What it
///     offers is the two things a conversation can be asked: <c>⏎</c> opens the thread and <c>m</c> takes the unread
///     mark off, both by the conversation's own id, which is not the id of any post in it (CONTEXT.md).
///     <para>
///         The last post is drawn under each row rather than summarised, because it is the thing that tells one
///         conversation from another, and drawing it through <see cref="PostLines" /> is what stops a message read
///         here and the same message read in the thread looking like two different things.
///     </para>
/// </remarks>
public sealed class DirectMessagesScreen(IReadOnlyList<Conversation> conversations, string? notice = null) : Screen
{
    /// <summary>
    ///     What a conversation whose posts have all been taken down says. The alternative is a heading with nothing
    ///     under it, which reads as a screen that went wrong rather than a conversation that was emptied.
    /// </summary>
    private const string NothingLeft = "Nothing left in this conversation.";

    private readonly List<Conversation> _conversations = [.. conversations];

    /// <inheritdoc />
    public override string Crumb => "direct messages";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
    [
        new("j/k", "conversation"),
        new("⏎", "open"),
        new("m", "mark read"),
        new("tab", "destination"),
        new("?", "keys"),
    ];

    /// <summary>Which conversation is picked out, as an index into what is on screen.</summary>
    public int At { get; private set; }

    /// <summary>The conversations, most recently spoken in first.</summary>
    public IReadOnlyList<Conversation> Conversations => _conversations;

    /// <summary>
    ///     Something the shell has to say about the list rather than about anything on it — that nobody has written, or
    ///     that a rate limit cut the listing short.
    /// </summary>
    public string? Notice { get; } = notice;

    /// <summary>
    ///     How many of them have something unread in them, which is what the rail's badge says. Counted from the list
    ///     itself so that the badge and the rows under it cannot come to disagree.
    /// </summary>
    public int Unread => _conversations.Count(conversation => conversation.Unread);

    /// <summary>
    ///     The conversation picked out, or <see langword="null" /> where nobody has written. What <c>⏎</c> opens and
    ///     <c>m</c> marks read, named by its own id.
    /// </summary>
    public Conversation? PickedConversation => _conversations.Count == 0 ? null : _conversations[At];

    /// <inheritdoc />
    public override void Move(int by)
    {
        if (_conversations.Count > 0)
        {
            At = PickedPosts.Clamped(At, by, _conversations.Count - 1);
        }
    }

    /// <summary>Puts <paramref name="conversation" /> in place of the copy this screen is holding, once it changed.</summary>
    public void Marked(Conversation conversation)
    {
        for (var at = 0; at < _conversations.Count; at++)
        {
            if (_conversations[at].Id == conversation.Id)
            {
                _conversations[at] = conversation;
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>A mark put on a message in the thread shows on the row the thread was opened from.</remarks>
    public override void Replace(Post post)
    {
        for (var at = 0; at < _conversations.Count; at++)
        {
            if (_conversations[at].Latest?.Id == post.Id)
            {
                _conversations[at] = _conversations[at] with { Latest = post };
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The conversation stays and loses its last post, which is what a conversation whose posts have been taken
    ///     down looks like — it is still there to be read or written to, and saying so is more honest than dropping it.
    /// </remarks>
    public override void Remove(string postId)
    {
        for (var at = 0; at < _conversations.Count; at++)
        {
            if (_conversations[at].Latest?.Id == postId)
            {
                _conversations[at] = _conversations[at] with { Latest = null };
            }
        }
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

        for (var at = 0; at < _conversations.Count; at++)
        {
            var conversation = _conversations[at];

            lines.Add(With(conversation, room).After(PickedPosts.Gutter(at == At)));

            if (conversation.Latest is { } latest)
            {
                foreach (var line in PostLines.Feed(latest, Math.Max(1, room - 2), revealed: false, now))
                {
                    lines.Add(line.After(PickedPosts.Gutter(at == At), new Span("  ", Role.Body)));
                }
            }
            else
            {
                lines.Add(Line.Of(NothingLeft, Role.Muted).After(PickedPosts.Gutter(at == At), new Span("  ", Role.Body)));
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    /// <summary>
    ///     Who the conversation is with, and whether anything in it is unread. The mark is the word rather than a
    ///     glyph: this client's glyphs already say who can see a post, and a second circle beside <c>●</c> would be one
    ///     mark too many to tell apart at a glance.
    /// </summary>
    public static Line With(Conversation conversation, int width)
    {
        // An instance says who a conversation is with rather than who is having it, so one with nobody in it is one
        // whose only other account has been taken down — said out loud rather than drawn as an empty row.
        var with = conversation.With.Count == 0
            ? "nobody"
            : string.Join(", ", conversation.With.Select(account => $"@{account}"));

        if (!conversation.Unread)
        {
            return Line.Of(TextWrap.Clip(with, width), Role.BylineHandle);
        }

        const string mark = "unread";

        var who = TextWrap.Clip(with, Math.Max(0, width - mark.Length - 1));

        return Line.Of([
            new Span(who, Role.BylineHandle),
            new Span(new string(' ', Math.Max(1, width - who.Length - mark.Length)), Role.Body),
            new Span(mark, Role.RailUnread),
        ]);
    }
}
