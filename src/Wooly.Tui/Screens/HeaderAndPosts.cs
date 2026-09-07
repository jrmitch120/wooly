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
public sealed class HeaderAndPosts(Account account, PostList posts) : IPicked
{
    /// <inheritdoc />
    /// <remarks>Nought is the header block, and one up are the posts in the order they are drawn.</remarks>
    public int At { get; private set; }

    /// <summary>Whether what is picked out is the header block rather than one of the posts.</summary>
    public bool OnHeader => At == 0;

    /// <summary>The account being shown, which is what the header block draws.</summary>
    public Account Account { get; private set; } = account;

    /// <summary>
    ///     The post picked out, or <see langword="null" /> while the header is — which is what leaves <c>b</c>,
    ///     <c>f</c>, <c>d</c> and <c>r</c> with nothing to act on there, the way a follow notification already does.
    /// </summary>
    public Post? Picked => OnHeader ? null : posts.Out;

    /// <inheritdoc />
    /// <remarks>
    ///     Counted in <see cref="long" /> for the reason <see cref="Picked{T}.Move" /> is: <c>Home</c> and <c>End</c>
    ///     ask to move by the largest step there is.
    /// </remarks>
    public void Move(int by) => Pick((int)Math.Clamp((long)At + by, 0, posts.Count));

    /// <inheritdoc />
    public void Pick(int at)
    {
        At = Math.Clamp(at, 0, posts.Count);

        if (!OnHeader)
        {
            posts.Pick(At - 1);
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

    /// <summary>The posts' own rows, numbered after the header and picked out only while the header is not.</summary>
    /// <param name="drawing">What this screen is being drawn in and under.</param>
    public IReadOnlyList<Line> PostRows(Drawing drawing) =>
        posts.Rows(drawing, new Ordinals(From: 1, Picked: At));
}
