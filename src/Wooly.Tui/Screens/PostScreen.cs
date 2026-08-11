using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One post whole, with what has been said in answer to it underneath. What <c>⏎</c> on a feed item opens onto.
/// </summary>
/// <remarks>
///     The post itself is the first thing picked out, and <c>j</c> walks down into the replies — so every key that
///     acts on a post (boost, favorite, reply, delete) means the same thing here as on the feed, and a reply can be
///     answered without leaving the thread it is in.
///     <para>
///         One list of the post and its replies rather than two, because that is what the reader walks: how the first
///         of them is drawn is a fact about the row and not about which container it came out of.
///     </para>
/// </remarks>
public sealed class PostScreen : Screen
{
    private readonly Picked<Post> _posts;

    /// <param name="post">The post this screen is about, which is the first thing on it.</param>
    /// <param name="replies">What has been said in answer to it, oldest first.</param>
    public PostScreen(Post post, IReadOnlyList<Post> replies) => _posts = new Picked<Post>([post, .. replies]);

    /// <inheritdoc />
    public override string Crumb => $"post by @{(Post.Boosted ?? Post).Account}";

    /// <inheritdoc />
    /// <remarks>
    ///     <c>⏎</c> comes off the row while the post itself is picked out, since there is nothing for it to open —
    ///     announcing a key and then refusing it reads as a shell that missed the press (#48).
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys
    {
        get
        {
            var keys = PostKeys.Around(new KeyHint("j/k", "post · replies"), new KeyHint("esc", "back"));

            return Opens is null ? [.. keys.Where(key => key != PostKeys.Opening)] : keys;
        }
    }

    /// <summary>Which of the post and its replies is picked out: 0 is the post, and the rest are the answers in order.</summary>
    public int At => _posts.At;

    /// <summary>The post this screen is about.</summary>
    public Post Post => _posts.All[0];

    /// <summary>What has been said in answer to it, oldest first.</summary>
    public IReadOnlyList<Post> Replies => [.. _posts.All.Skip(1)];

    /// <inheritdoc />
    public override Post? Picked => _posts.Out;

    /// <inheritdoc />
    /// <remarks>
    ///     Only an answer. The post at index 0 is the one this screen is about and is already whole on it, so drilling
    ///     into it would push a copy of this same screen and put a place nobody went on the breadcrumb (#48) — which
    ///     with the post and its replies in one list is a fact about where in the list the pick is.
    /// </remarks>
    public override Post? Opens => At == 0 ? null : Picked;

    /// <inheritdoc />
    protected override IPicked Walking => _posts;

    /// <inheritdoc />
    public override void Replace(Post post) => _posts.Rewrite(held => PostChange.Replaced(held, post));

    /// <inheritdoc />
    /// <remarks>
    ///     Only the replies go. A post screen showing a post that is no longer there is a screen about nothing, and
    ///     the shell walks out of it rather than leaving this one to draw a thread with no head to it.
    /// </remarks>
    public override void Remove(string postId)
    {
        // Read before the list is walked rather than inside the asking, so that what counts as the head of the thread
        // cannot depend on how far a removal has already got through it.
        var head = Post;

        _posts.Remove(held => held.Id != head.Id && PostChange.Names(held, postId));
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        // A blank rather than the rule that separates two replies below: the heading is already a ruled row, and two
        // of them stacked would read as a boundary twice over.
        var lines = new List<Line>(_posts.RowsOf(0, width, Draw))
        {
            Line.Blank,
            Line.Of(Heading(width), Role.Muted),
            Line.Blank,
        };

        if (_posts.Count == 1)
        {
            // Indented into step with the rows above it, which are a gutter column wider than they are drawn. Part of
            // nothing: there is no reply here for it to be part of.
            lines.Add(Line.Of("Nobody has answered this yet.", Role.Muted).After(new Span(" ", Role.Body)));

            return lines;
        }

        for (var at = 1; at < _posts.Count; at++)
        {
            lines.AddRange(_posts.RowsOf(at, width, Draw));
            lines.Add(Line.Rule(width));
        }

        return lines;

        IReadOnlyList<Line> Draw(Post post, int at, int room) => at == 0
            ? PostLines.Whole(post, room, Revealed.Has(post), now, pictures, hideDrawnCaption, ReferenceOn(at))
            : PostLines.Feed(post, room, Revealed.Has(post), now, pictures, hideDrawnCaption, ReferenceOn(at));
    }

    private string Heading(int width) =>
        TextWrap.Clip(_posts.Count == 1 ? "── replies ──" : $"── {_posts.Count - 1} replies ──", width);
}
