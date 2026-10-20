using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a reader walks on an account screen: the header block, and then the posts under it. The first walk in this
///     shell whose things are not all of one kind, and whose first one is not a post at all (#179, ADR-0019).
/// </summary>
/// <remarks>
///     The pick opens on the header rather than on the first post, because the screen is about the person: landing
///     below them would make the bio something a reader walks back to, and opening on it puts a verified link one
///     <c>→</c> away on arrival.
///     <para>
///         One index rather than two, which is the whole reason this is a thing of its own: the screen has one pick,
///         and a header that knew whether it was picked while the post list kept an index of its own would be a screen
///         that could draw two selections or none. The list is told where it stands in the screen's numbering
///         (<see cref="Ordinals" />) and answers accordingly; nothing else here numbers anything.
///     </para>
///     <para>
///         The header block is not a <see cref="Picked{T}" /> of one. It is one thing that is always there — there is
///         no list of headers to walk, nothing to add to or take off, and no ordinal to clamp — so what it borrows is
///         the stamping alone (<see cref="Stamped" />), which is the part that has to agree with the walk.
///     </para>
/// </remarks>
/// <param name="account">The account being shown, as the instance last answered about them.</param>
/// <param name="posts">The posts of theirs that were read, which this walks after the header.</param>
/// <param name="editable">
///     Whether the header block ends on the "edit public profile" row, walked to after the header and before the posts —
///     on the own account alone (#406).
/// </param>
public sealed class HeaderAndPosts(Account account, PostList posts, bool editable = false) : IPicked
{
    /// <summary>Where the "edit public profile" row is in the walk, on the own account: straight after the header.</summary>
    private const int EditRow = 1;

    /// <inheritdoc />
    /// <remarks>
    ///     Nought is the header block, one the "edit public profile" row where there is one, and the posts after that in
    ///     the order they are drawn.
    /// </remarks>
    public int At { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    ///     The posts and the header block, which is why an account screen is never empty: there is always somebody to
    ///     be looking at, whether or not they have posted anything (#195).
    /// </remarks>
    public int Count => posts.Count + FirstPost;

    /// <summary>Whether what is picked out is the header block rather than one of the posts.</summary>
    public bool OnHeader => At == 0;

    /// <summary>Whether what is picked out is the "edit public profile" row (#406).</summary>
    public bool OnTheEditRow => editable && At == EditRow;

    /// <summary>Whether the thing a row is part of (<see cref="Line.Item" />) is the "edit public profile" row.</summary>
    public bool IsTheEditRow(int? item) => editable && item == EditRow;

    /// <summary>The account being shown, which is what the header block draws.</summary>
    public Account Account { get; private set; } = account;

    /// <summary>
    ///     The post picked out, or <see langword="null" /> while the header is — which is what leaves <c>b</c>,
    ///     <c>f</c>, <c>d</c> and <c>r</c> with nothing to act on there, the way a follow notification already does. The
    ///     "edit public profile" row is the same: nothing a post key could act on.
    /// </summary>
    public Post? Picked => At < FirstPost ? null : posts.Out;

    /// <summary>Where the first post is in the walk: after the header, and after the "edit public profile" row where there is one.</summary>
    private int FirstPost => editable ? EditRow + 1 : 1;

    /// <inheritdoc />
    /// <remarks>
    ///     Counted in <see cref="long" /> for the reason <see cref="Picked{T}.Move" /> is: <c>Home</c> and <c>End</c>
    ///     ask to move by the largest step there is.
    /// </remarks>
    public void Move(int by) => Pick((int)Math.Clamp((long)At + by, 0, Count - 1));

    /// <inheritdoc />
    public void Pick(int at)
    {
        At = Math.Clamp(at, 0, Count - 1);

        if (At >= FirstPost)
        {
            posts.Pick(At - FirstPost);
        }
    }

    /// <summary>Puts the account as the instance now has it in place of the copy this is holding, after a tie changed it.</summary>
    public void Stands(Account stands) => Account = stands;

    /// <summary>
    ///     Takes the post <paramref name="postId" /> names off the list under the header, and brings the pick back
    ///     inside what is left of it.
    /// </summary>
    /// <remarks>
    ///     Asked here rather than of the list, because the pick is this one and a list re-clamping an index of its own
    ///     would leave the two saying different things: the screen would go on offering the keys a post answers to
    ///     while no row on it was drawn as picked out. The screen has one pick, and everything that can move it says so
    ///     here.
    /// </remarks>
    /// <param name="postId">The id of the post that was deleted.</param>
    public void Remove(string postId)
    {
        posts.Remove(postId);

        Pick(At);
    }

    /// <summary>
    ///     The header block's rows, behind the gutter that says whether it is what is picked out and each naming it as
    ///     the screen's first thing.
    /// </summary>
    /// <param name="draw">How the block itself draws, in the room its gutter leaves it.</param>
    /// <param name="width">How wide the content region is — 61 at an 80-column terminal.</param>
    public IReadOnlyList<Line> HeaderRows(Draws<Account> draw, int width) =>
        Stamped.Rows(draw(Account, 0, Stamped.Room(width)), 0, OnHeader);

    /// <summary>
    ///     The "edit public profile" row and the blank under it, behind the gutter and numbered as the walk's second
    ///     thing — or nothing, on anybody's account but the own one (#406).
    /// </summary>
    /// <param name="draw">How the row draws, in the room its gutter leaves it.</param>
    /// <param name="width">How wide the content region is.</param>
    public IReadOnlyList<Line> EditRows(Func<int, IReadOnlyList<Line>> draw, int width) =>
        editable ? Stamped.Rows(draw(Stamped.Room(width)), EditRow, OnTheEditRow) : [];

    /// <summary>
    ///     One run of the posts as rows, numbered after the header and picked out only while the header is not.
    /// </summary>
    /// <remarks>
    ///     A run rather than the lot, because the screen draws two of them with a heading between (#182) — and the
    ///     numbering is this walk's either way, which is the whole reason the screen asks here rather than of the list.
    /// </remarks>
    /// <param name="drawing">What this screen is being drawn in and under.</param>
    /// <param name="from">Which post the run begins at, counted from the first on the list rather than the header.</param>
    /// <param name="howMany">How many posts are in it.</param>
    public IReadOnlyList<Line> PostRows(Drawing drawing, int from, int howMany) =>
        posts.Run(drawing, new Ordinals(From: FirstPost, Picked: At), from, howMany);
}
