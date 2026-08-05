using Wooly.Core.Conversations;
using Wooly.Core.Posts;
using Wooly.Tui.Media;
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
    private readonly List<Conversation> _conversations = [.. conversations];

    /// <inheritdoc />
    public override string Crumb => "direct messages";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
    [
        new("j/k", "conversation"),
        PostKeys.Scrolling,
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

    /// <inheritdoc />
    public override void Pick(int at)
    {
        if (_conversations.Count > 0)
        {
            At = PickedPosts.Chosen(at, _conversations.Count - 1);
        }
    }

    /// <summary>
    ///     Puts <paramref name="conversation" /> in place of the copy this screen is holding, once it has changed —
    ///     marked read, or spoken in again from the thread it opened onto.
    /// </summary>
    public void Marked(Conversation conversation) =>
        Rewrite(held => held.Id == conversation.Id, _ => conversation);

    /// <inheritdoc />
    /// <remarks>
    ///     A mark put on a message in the thread shows on the row the thread was opened from: the two screens are on
    ///     the stack together, and the shell hands a changed post to both.
    /// </remarks>
    public override void Replace(Post post) =>
        Rewrite(held => held.Latest?.Id == post.Id, held => held with { Latest = post });

    /// <inheritdoc />
    /// <remarks>
    ///     The conversation stays and loses its last post, which is what a conversation whose posts have been taken
    ///     down looks like — it is still there to be read or written to, and saying so is more honest than dropping it.
    /// </remarks>
    public override void Remove(string postId) =>
        Rewrite(held => held.Latest?.Id == postId, held => held with { Latest = null });

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now, IPictures? pictures = null)
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

            lines.Add(ConversationLines.With(conversation, room).After(PickedPosts.Gutter(at == At)).PartOf(at));

            if (conversation.Latest is { } latest)
            {
                foreach (var line in PostLines.Feed(latest, Math.Max(1, room - 2), revealed: false, now, pictures))
                {
                    lines.Add(line.After(PickedPosts.Gutter(at == At), new Span("  ", Role.Body)).PartOf(at));
                }
            }
            else
            {
                lines.Add(Line.Of(ConversationLines.NothingLeft, Role.Muted)
                              .After(PickedPosts.Gutter(at == At), new Span("  ", Role.Body))
                              .PartOf(at));
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    /// <summary>
    ///     Puts <paramref name="changed" /> in place of every conversation <paramref name="which" /> picks out. Said
    ///     once, because a conversation on this screen changes in three ways — marked read, its last post replaced,
    ///     its last post taken down — and three walks of the same list would be three chances to walk it differently.
    /// </summary>
    private void Rewrite(Func<Conversation, bool> which, Func<Conversation, Conversation> changed)
    {
        for (var at = 0; at < _conversations.Count; at++)
        {
            if (which(_conversations[at]))
            {
                _conversations[at] = changed(_conversations[at]);
            }
        }
    }
}
