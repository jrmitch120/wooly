using Wooly.Core.Conversations;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
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
    // Named by the conversation's own id, which is not the id of any post in it (CONTEXT.md) — and is what a refresh
    // puts the reader back on (#84).
    private readonly Picked<Conversation> _conversations = new(conversations);

    /// <inheritdoc />
    public override string Crumb => "Direct messages";

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new("j/k", "conversation"),
        new("⏎", "open", NeedsAPick: true),
        new("m", "mark read", NeedsAPick: true),
        Refreshing,
        .. PostKeys.Leaving(new KeyHint("tab", "destination")),
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

    /// <inheritdoc />
    /// <remarks>
    ///     The one screen where this is not <see cref="Screen.Picked" />, which here is nothing: a row is a
    ///     conversation rather than a post, so none of the keys that act on a post act here. The last thing said is
    ///     still drawn through <see cref="PostLines" /> though, and an address in it is as walkable as an address
    ///     anywhere else (#83).
    /// </remarks>
    protected override Post? Referencing => PickedConversation?.Latest;

    /// <summary>
    ///     What this screen's own <c>⏎</c> does: opens the picked conversation — the thread its last post is in, oldest
    ///     first. Named by the conversation's own id, which is not the id of any post in it (CONTEXT.md).
    /// </summary>
    /// <remarks>
    ///     Reading one does not mark it read (ADR-0013). A client that cleared the mark on the way past would make
    ///     "what have I not read" unanswerable for anything that looked afterwards, so <c>m</c> is what takes it off and
    ///     nothing else does.
    /// </remarks>
    public override Task Answer(Verb verb, Reach reach) => (verb, PickedConversation) switch
    {
        (Verb.OpenConversation, { } picked) => reach.Open(new Subject.Conversation(picked.Id)),
        _ => Task.CompletedTask,
    };

    /// <inheritdoc />
    /// <remarks>
    ///     A mark put on a message in the thread shows on the row the thread was opened from: the two screens are on
    ///     the stack together, and both hear it. A message deleted leaves its conversation standing without a last
    ///     post, which is what a conversation whose posts have been taken down looks like — it is still there to be
    ///     read or written to, and saying so is more honest than dropping it. A reply written inside a conversation is
    ///     its last post now, whatever in the thread it answered.
    /// </remarks>
    public override bool Heard(Change change)
    {
        switch (change)
        {
            case Change.PostChanged(var post):
                _conversations.Rewrite(held => held.Latest?.Id == post.Id ? held with { Latest = post } : held);

                break;

            case Change.PostGone(var id):
                _conversations.Rewrite(held => held.Latest?.Id == id ? held with { Latest = null } : held);

                break;

            case Change.PostSent(var post, { } within):
                _conversations.Rewrite(held => held.Id == within ? held with { Latest = post } : held);

                break;

            case Change.ConversationMarked(var marked):
                _conversations.Rewrite(held => held.Id == marked.Id ? marked : held);

                break;
        }

        return false;
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

        lines.AddRange(_conversations.Rows(drawing.Width, Draw));

        return lines;

        // Who it is with, and under it the last thing said — indented, so that the row naming the conversation reads
        // as the heading of the two rather than as another message in the list.
        IReadOnlyList<Line> Draw(Conversation conversation, int at, int room)
        {
            var indent = new Span("  ", Role.Body);

            var said = conversation.Latest is { } latest
                // A reference the reader has walked to, and nothing else: what this screen picks out is a
                // conversation, so there is no post here for x to have asked past. Deliberately not ReadingOf, which
                // would be asking a question this screen cannot answer — and the same fact is why the row naming x is
                // off. A warned message stands behind its warning here with nothing offering to lift it, because the
                // way to read it is to open the conversation, which is what ⏎ already does (#120).
                ? PostLines.Feed(
                    latest,
                    drawing.In(Math.Max(1, room - 2)),
                    new Reading(Reference: ReferenceOn(at)),
                    saysHowToAskPast: false)
                : [Line.Of(ConversationLines.NothingLeft, Role.Muted)];

            return [ConversationLines.With(conversation, room), .. said.Select(line => line.After(indent))];
        }
    }
}
