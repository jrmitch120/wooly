using Wooly.Core.Posts;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Screens;

/// <summary>
///     The posts on one screen with one of them picked out: the walk, what a mark or a deletion does to them, and the
///     rows they are drawn on.
/// </summary>
/// <remarks>
///     What <see cref="Picked{T}" /> is for anything, said for posts in particular — because "a list of posts on a
///     screen" is the thing four screens hold, and everything about it beyond the index used to be copied into each of
///     them (#99). A change to what a post list <em>is</em> — how the copy a mark changed is found, what a deletion
///     takes off, how a row is drawn — is one edit here rather than four edits nothing makes fail to compile.
///     <para>
///         Built from the screen holding it rather than standing alone, because a row takes what this reader has done
///         to the post on it (<see cref="Screen.ReadingOf" />): which warnings they have asked past is the screen's,
///         and so is which reference they have walked to. The screen still answers those questions; this is what asks
///         them — only while it is drawing, which is what makes a screen safe to hand over as it is being built.
///     </para>
///     <para>
///         No new word for it: what this holds is <b>Picked</b> as CONTEXT.md already defines it, and the posts are
///         posts. The forwarding members are the price of naming the pair once — a screen that reached past this to
///         the list inside it could pick one thing and draw another.
///     </para>
/// </remarks>
/// <param name="screen">The screen holding this list, which is what a row's <see cref="Reading" /> is asked of.</param>
/// <param name="posts">The posts, in the order they are drawn and walked.</param>
public sealed class PostList(Screen screen, IReadOnlyList<Post> posts) : IPicked
{
    // Named by the post's own id, which is what a refresh matches the reader's place against — a boost by the id it
    // was passed on under, since that is the thing on the list rather than the post inside it (#84).
    private readonly Picked<Post> _posts = new(posts);

    /// <inheritdoc />
    public int At => _posts.At;


    /// <summary>The posts, in the order they are drawn and walked.</summary>
    public IReadOnlyList<Post> All => _posts.All;

    /// <summary>How many there are.</summary>
    public int Count => _posts.Count;

    /// <summary>The post picked out, or <see langword="null" /> where the screen has none on it.</summary>
    public Post? Out => _posts.Out;

    /// <inheritdoc />
    public void Move(int by) => _posts.Move(by);

    /// <inheritdoc />
    public void Pick(int at) => _posts.Pick(at);


    /// <inheritdoc cref="Picked{T}.Add" />
    public void Add(Post post) => _posts.Add(post);

    /// <summary>
    ///     Puts <paramref name="post" /> in place of the copy this list is holding, after a mark changed it — inside a
    ///     boost of it where that is what is held, since a boost is the same post as far as a mark goes.
    /// </summary>
    public void Replace(Post post) => _posts.Rewrite(held => PostChange.Replaced(held, post));

    /// <summary>Takes the post <paramref name="postId" /> names, or a boost of it, off the list.</summary>
    /// <param name="postId">The id of the post that was deleted.</param>
    /// <param name="keeping">
    ///     A post to leave where it is even when it is the one named — the post a post screen is about, which is
    ///     already whole on it. A thread with no head to it is a screen about nothing, and the shell walks out of it
    ///     rather than leaving the screen to draw one. Said as a post rather than as a question asked of each one, so
    ///     that what counts as the head cannot depend on how far a removal has already got through the list.
    /// </param>
    public void Remove(string postId, Post? keeping = null) =>
        _posts.Remove(held => held.Id != keeping?.Id && PostChange.Names(held, postId));

    /// <summary>The posts as feed rows, each behind the gutter that says whether it is the one picked out.</summary>
    /// <remarks>
    ///     The one place <see cref="PostLines.Feed" /> is reached from a list of posts. It was the same line in three
    ///     screens, and a fourth screen drawing its rows some other way would have been nothing anybody could see
    ///     until they read all four (#99).
    ///     <para>
    ///         A list of <em>posts</em>, which is the whole of the qualifier: search results and notifications draw a
    ///         post too, but each reaches it through a thing of its own — a result, a notification — and neither is a
    ///         list of posts to walk. #95 named those as the sites this does not fit, and that finding stands.
    ///     </para>
    /// </remarks>
    /// <param name="drawing">
    ///     What this screen is being drawn in and under, which a row is handed narrowed by its own gutter (#148).
    /// </param>
    public IReadOnlyList<Line> Rows(Drawing drawing) => _posts.Rows(drawing.Width, Feed(drawing));

    /// <inheritdoc cref="Rows" />
    /// <summary>The <paramref name="at" />th post's rows on their own, with no rule after them.</summary>
    /// <remarks>
    ///     For the post screen, which puts a heading of its own between the post it is about and the answers to it.
    ///     Splicing rows between posts is that screen's; stamping the posts' own rows is still not.
    /// </remarks>
    public IReadOnlyList<Line> RowsOf(int at, Drawing drawing) => _posts.RowsOf(at, drawing.Width, Feed(drawing));

    /// <summary>
    ///     The <paramref name="at" />th post's rows, drawn the screen's own way rather than as a feed row — which one
    ///     screen wants for one post: the post screen draws the post it is about <see cref="PostLines.Whole" />.
    /// </summary>
    /// <remarks>
    ///     The drawing is the screen's and the stamping is still the list's, so a screen taking this one still cannot
    ///     number its rows differently from the walk.
    ///     <para>
    ///         Here rather than on <see cref="Rows" />, which is where #99 proposed it: the one screen that draws a
    ///         post its own way is also the one that splices a heading between that post and the rest, so it asks for
    ///         its rows one at a time and never has a whole list to hand a drawing to.
    ///     </para>
    /// </remarks>
    public IReadOnlyList<Line> RowsOf(int at, int width, Draws<Post> draw) => _posts.RowsOf(at, width, draw);

    /// <summary>How a post on this list draws itself, with what its reader has done to it filled in.</summary>
    /// <remarks>
    ///     The drawing goes through as it came, narrowed to the room the gutter leaves: this list adds nothing to it
    ///     and takes nothing out of it, so a fact the shell puts on one reaches a row here without this file being
    ///     touched (#148).
    /// </remarks>
    private Draws<Post> Feed(Drawing drawing) =>
        (post, at, room) => PostLines.Feed(post, drawing.In(room), screen.ReadingOf(post, at));
}
