using Wooly.Core.Conversations;
using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One conversation and what was said in it, oldest first — the order it happened in, because a reply after the
///     thing it answers is the only order it makes sense in. What <c>⏎</c> on the conversations list opens onto.
/// </summary>
/// <remarks>
///     Every message here is an ordinary post that went out direct, so every key that acts on one means the same thing
///     it means on a feed — which is what lets a message be answered without leaving the thread it is in.
///     <para>
///         What it shows is the thread the conversation's last post is in, which an instance's own grouping makes
///         narrower than "everything in the conversation" (ADR-0013). That is the API's shape rather than this
///         screen's, and an older root is still reachable by its own post id.
///     </para>
/// </remarks>
public sealed class ConversationScreen(ConversationThread thread) : Screen
{
    private readonly Picked<Post> _posts = new(thread.Posts);

    /// <inheritdoc />
    public override string Crumb => $"with {ConversationLines.Who(Conversation)}";

    /// <inheritdoc />
    /// <remarks>
    ///     <c>m</c> in front of the shared keys, because it is the one a reader can find on no other screen and the
    ///     status row is cut off at the right (<c>docs/tui-shell.md</c>).
    /// </remarks>
    public override IReadOnlyList<KeyHint> Keys =>
        PostKeys.Around(
            new KeyHint("j/k", "message"),
            [new KeyHint("m", "mark read")],
            new KeyHint("esc", "back"));

    /// <summary>The conversation itself: its id, who it is with, and whether it is still unread.</summary>
    public Conversation Conversation { get; private set; } = thread.Conversation;

    /// <summary>What was said in it, oldest first.</summary>
    public IReadOnlyList<Post> Posts => _posts.All;

    /// <summary>Which message is picked out, as an index into the thread.</summary>
    public int At => _posts.At;

    /// <inheritdoc />
    public override Post? Picked => _posts.Out;

    /// <inheritdoc />
    protected override IPicked Walking => _posts;

    /// <summary>Puts the conversation as the instance now has it in place of the one this screen opened with.</summary>
    public void Marked(Conversation conversation) => Conversation = conversation;

    /// <summary>
    ///     Puts a message that has just been sent at the end of the thread, so a reply lands where the reader is
    ///     looking rather than only in the next read of the conversation.
    /// </summary>
    /// <remarks>
    ///     It is the conversation's last word as well as the thread's last message, so the conversation itself moves
    ///     with it — which is what the shell hands back to the list this thread was opened from.
    /// </remarks>
    public void Said(Post post)
    {
        _posts.Add(post);

        Conversation = Conversation with { Latest = post };
    }

    /// <inheritdoc />
    public override void Replace(Post post) => _posts.Rewrite(held => PostChange.Replaced(held, post));

    /// <inheritdoc />
    public override void Remove(string postId) => _posts.Remove(held => PostChange.Names(held, postId));

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var lines = new List<Line>
        {
            ConversationLines.With(Conversation, width),
            Line.Blank,
        };

        if (_posts.Count == 0)
        {
            lines.Add(Line.Of(ConversationLines.NothingLeft, Role.Muted));

            return lines;
        }

        lines.AddRange(_posts.Rows(
            width,
            (post, _, room) => PostLines.Feed(post, room, Revealed.Has(post), now, pictures, hideDrawnCaption)));

        return lines;
    }
}
