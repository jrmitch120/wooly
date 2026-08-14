using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One post whole, with what it answers above it and what has been said in answer to it underneath. What <c>⏎</c>
///     on a feed item opens onto.
/// </summary>
/// <remarks>
///     The post itself is what is picked out on arrival — not the top of the thread, which is only how the post came
///     to be said — and <c>j</c> and <c>k</c> walk out of it either way. So every key that acts on a post (boost,
///     favorite, reply, delete) means the same thing here as on the feed, and anything in the thread can be answered
///     without leaving it.
///     <para>
///         One list of the whole thread rather than three, because that is what the reader walks: how the post in the
///         middle of it is drawn is a fact about the row and not about which container it came out of. Which row that
///         is, though, is not a constant — an ancestor deleted while the screen is open shifts every row below it — so
///         the screen is about a <em>post</em> and finds it by id, rather than about a place in a list (#86).
///     </para>
/// </remarks>
public sealed class PostScreen : Screen
{
    private readonly PostList _posts;

    /// <summary>
    ///     The id of the post this screen is about, which is what tells it apart from everything else on the list.
    /// </summary>
    /// <remarks>
    ///     The id rather than the post, because the copy being held is rewritten as marks land on it
    ///     (<see cref="Replace" />) — and rather than the index, because <see cref="Remove" /> can take a post off
    ///     above this one. Both of the places index 0 used to stand for the subject were wrong the moment anything was
    ///     drawn above it.
    /// </remarks>
    private readonly string _postId;

    /// <param name="post">The post this screen is about, which is the thing picked out on it.</param>
    /// <param name="thread">What it answers, above it, and what has been said in answer to it, below.</param>
    public PostScreen(Post post, PostThread thread)
    {
        _postId = post.Id;
        _posts = new PostList(this, [.. thread.Ancestors, post, .. thread.Replies]);
        _posts.Pick(thread.Ancestors.Count);
    }

    /// <inheritdoc />
    public override string Crumb => $"post by @{(Post.Boosted ?? Post).Account}";

    /// <inheritdoc />
    /// <remarks>
    ///     <c>⏎</c> comes off the row while the post itself is picked out, since there is nothing for it to open —
    ///     announcing a key and then refusing it reads as a shell that missed the press (#48).
    ///     <para>
    ///         The walk is named for the thread rather than for "post · replies", which stopped being all of what
    ///         <c>j</c> and <c>k</c> reach the moment the chain above the post joined the same list (#86).
    ///     </para>
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys
    {
        get
        {
            var keys = PostKeys.Around(
                new KeyHint("j/k", "thread"),
                [Refreshing],
                new KeyHint("esc", "back"));

            return Opens is null ? [.. keys.Where(key => key != PostKeys.Opening)] : keys;
        }
    }

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <summary>
    ///     Which post on the screen is picked out: the ancestors in order, then the post itself, then the answers.
    /// </summary>
    public int At => _posts.At;

    /// <summary>The post this screen is about.</summary>
    public Post Post => _posts.All[Subject];

    /// <summary>What it answers, the root of the thread first and the post it directly answers last.</summary>
    public IReadOnlyList<Post> Ancestors => [.. _posts.All.Take(Subject)];

    /// <summary>What has been said in answer to it, oldest first.</summary>
    public IReadOnlyList<Post> Replies => [.. _posts.All.Skip(Subject + 1)];

    /// <summary>
    ///     Where on the list the post this screen is about stands, which is how many ancestors are above it.
    /// </summary>
    /// <remarks>
    ///     Worked out afresh rather than counted once at construction, because a deletion takes a post off the list
    ///     and everything below it moves up. The post itself never goes — <see cref="Remove" /> keeps it — so the
    ///     search always finds it, and a boost of it is named by its own id, which is the id the row carries.
    ///     <para>
    ///         The top of the list where it somehow does not: a screen that has lost the post it is about is a screen
    ///         about nothing and the shell walks out of it, and falling back is one row drawn wrong where throwing
    ///         from a draw is a client that stops drawing.
    ///     </para>
    /// </remarks>
    private int Subject =>
        _posts.All.TakeWhile(post => post.Id != _postId).Count() is var at && at < _posts.Count ? at : 0;

    /// <inheritdoc />
    public override Post? Picked => _posts.Out;

    /// <inheritdoc />
    /// <remarks>
    ///     Anything but the post this screen is about, which is already whole on it: drilling into that one would push
    ///     a copy of this same screen and put a place nobody went on the breadcrumb (#48). An ancestor is a whole post
    ///     of its own and opens like any other — the thread above this post is not this post (#86).
    /// </remarks>
    public override Post? Opens => At == Subject ? null : Picked;

    /// <inheritdoc />
    protected override IPicked Walking => _posts;

    /// <inheritdoc />
    public override void Replace(Post post) => _posts.Replace(post);

    /// <inheritdoc />
    /// <remarks>
    ///     Anything on the thread but the post itself: an ancestor deleted goes the same way an answer does, and the
    ///     rows below it move up. A post screen showing a post that is no longer there is a screen about nothing, and
    ///     the shell walks out of it rather than leaving this one to draw a thread with no head to it — which is also
    ///     what keeps <see cref="Subject" /> able to find it.
    /// </remarks>
    public override void Remove(string postId) => _posts.Remove(postId, Post);

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(
        int width,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var subject = Subject;
        var lines = new List<Line>();

        // What the post answers, oldest first, with the rule that separates two posts between them — and none after
        // the last of them, because the heading below is a ruled row of its own.
        for (var at = 0; at < subject; at++)
        {
            if (at > 0)
            {
                lines.Add(Line.Rule(width));
            }

            lines.AddRange(_posts.RowsOf(at, width, now, pictures, hideDrawnCaption));
        }

        if (subject > 0)
        {
            lines.AddRange(Headed($"── {subject} up ──", width));
        }

        lines.AddRange(_posts.RowsOf(subject, width, Whole));

        // A blank either side rather than the rule that separates two replies below: the heading is already a ruled
        // row, and two of them stacked would read as a boundary twice over.
        lines.AddRange(Headed(RepliesHeading(_posts.Count - subject - 1), width));

        if (subject == _posts.Count - 1)
        {
            // Indented into step with the rows above it, which are a gutter column wider than they are drawn. Part of
            // nothing: there is no reply here for it to be part of.
            lines.Add(Line.Of("Nobody has answered this yet.", Role.Muted).After(new Span(" ", Role.Body)));

            return lines;
        }

        for (var at = subject + 1; at < _posts.Count; at++)
        {
            lines.AddRange(_posts.RowsOf(at, width, now, pictures, hideDrawnCaption));
            lines.Add(Line.Rule(width));
        }

        return lines;

        // The one post on this screen drawn any way but as a feed item: everything else here is what the list draws
        // by default, and which of the two a row gets is settled by which of them asked for it rather than by an
        // ordinal inside a drawing that could answer differently.
        // Its own ↳ mark comes off it once — and only once — what the mark points at is drawn whole immediately
        // above, which is the whole of the reason to take it off (docs/tui-shell.md, #86). A reply whose chain came
        // back empty, because what it answered has been deleted or because the instance did not send it, keeps the
        // mark: there is nothing above it saying the same thing, and the row is then all the reader has.
        IReadOnlyList<Line> Whole(Post post, int at, int room) => PostLines.Whole(
            post,
            room,
            ReadingOf(post, at),
            now,
            pictures,
            hideDrawnCaption,
            saysWhatItAnswers: subject == 0);
    }

    /// <summary>A heading of this screen's own, standing clear of the posts either side of it.</summary>
    private static IEnumerable<Line> Headed(string heading, int width) =>
    [
        Line.Blank,
        Line.Of(TextWrap.Clip(heading, width), Role.Muted),
        Line.Blank,
    ];

    /// <summary>How many answered the post, or the bare word where nobody has.</summary>
    private static string RepliesHeading(int replies) => replies > 0 ? $"── {replies} replies ──" : "── replies ──";
}
