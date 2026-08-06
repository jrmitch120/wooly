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
    private readonly Picked<Conversation> _conversations = new(conversations);

    /// <inheritdoc />
    public override string Crumb => "direct messages";

    /// <inheritdoc />
    public override IReadOnlyList<KeyHint> Keys =>
    [
        new("j/k", "conversation"),
        new("⏎", "open"),
        new("m", "mark read"),
        PostKeys.Scrolling,
        new("tab", "destination"),
        new("?", "keys"),
    ];

    /// <summary>Which conversation is picked out, as an index into what is on screen.</summary>
    public int At => _conversations.At;

    /// <summary>The conversations, most recently spoken in first.</summary>
    public IReadOnlyList<Conversation> Conversations => _conversations.All;

    /// <summary>
    ///     Something the shell has to say about the list rather than about anything on it — that nobody has written, or
    ///     that a rate limit cut the listing short.
    /// </summary>
    public string? Notice { get; } = notice;

    /// <summary>
    ///     How many of them have something unread in them, which is what the rail's badge says. Counted from the list
    ///     itself so that the badge and the rows under it cannot come to disagree.
    /// </summary>
    public int Unread => _conversations.All.Count(conversation => conversation.Unread);

    /// <summary>
    ///     The conversation picked out, or <see langword="null" /> where nobody has written. What <c>⏎</c> opens and
    ///     <c>m</c> marks read, named by its own id.
    /// </summary>
    public Conversation? PickedConversation => _conversations.Out;

    /// <inheritdoc />
    protected override IPicked Walking => _conversations;

    /// <summary>
    ///     Puts <paramref name="conversation" /> in place of the copy this screen is holding, once it has changed —
    ///     marked read, or spoken in again from the thread it opened onto.
    /// </summary>
    public void Marked(Conversation conversation) =>
        _conversations.Rewrite(held => held.Id == conversation.Id ? conversation : held);

    /// <inheritdoc />
    /// <remarks>
    ///     A mark put on a message in the thread shows on the row the thread was opened from: the two screens are on
    ///     the stack together, and the shell hands a changed post to both.
    /// </remarks>
    public override void Replace(Post post) =>
        _conversations.Rewrite(held => held.Latest?.Id == post.Id ? held with { Latest = post } : held);

    /// <inheritdoc />
    /// <remarks>
    ///     The conversation stays and loses its last post, which is what a conversation whose posts have been taken
    ///     down looks like — it is still there to be read or written to, and saying so is more honest than dropping it.
    /// </remarks>
    public override void Remove(string postId) =>
        _conversations.Rewrite(held => held.Latest?.Id == postId ? held with { Latest = null } : held);

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(int width, DateTimeOffset now, IPictures? pictures = null)
    {
        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, width), Role.Muted));
            lines.Add(Line.Blank);
        }

        lines.AddRange(_conversations.Rows(width, Draw));

        return lines;

        // Who it is with, and under it the last thing said — indented, so that the row naming the conversation reads
        // as the heading of the two rather than as another message in the list.
        IReadOnlyList<Line> Draw(Conversation conversation, int at, int room)
        {
            var indent = new Span("  ", Role.Body);

            var said = conversation.Latest is { } latest
                ? PostLines.Feed(latest, Math.Max(1, room - 2), revealed: false, now, pictures)
                : [Line.Of(ConversationLines.NothingLeft, Role.Muted)];

            return [ConversationLines.With(conversation, room), .. said.Select(line => line.After(indent))];
        }
    }
}
