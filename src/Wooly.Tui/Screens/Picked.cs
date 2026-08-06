using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     How one thing on a screen draws itself: the thing, which ordinal it is, and the room left beside the gutter.
/// </summary>
/// <remarks>
///     A delegate the screen hands down rather than something the things themselves answer, because they are
///     <c>Wooly.Core</c> records — a post, an account, a conversation — and Core references nothing above it, so none
///     of them can be asked for a row. The ordinal is given because a list can draw its first thing differently from
///     the rest: a post screen shows the post it is about whole and its replies as feed items.
/// </remarks>
public delegate IReadOnlyList<Line> Draws<in T>(T thing, int at, int room);

/// <summary>What a screen's reader walks with <c>j</c> and <c>k</c>, said without saying what it is a list of.</summary>
/// <remarks>
///     What lets <see cref="Screen.Move" /> and <see cref="Screen.Pick" /> be the same two lines for every screen
///     instead of an override apiece. A screen with nothing to walk — the compose editor, the keymap, a notice — has
///     none of this rather than an empty one.
/// </remarks>
public interface IPicked
{
    /// <summary>Moves what is picked out by <paramref name="by" /> things, stopping at either end.</summary>
    void Move(int by);

    /// <summary>Picks the <paramref name="at" />th thing out, stopping at either end.</summary>
    void Pick(int at);
}

/// <summary>
///     The things on one screen with one of them picked out, and the rows they are drawn on. Which of the things the
///     reader has walked to with <c>j</c> and <c>k</c>, and what every key that acts on something acts on (CONTEXT.md).
/// </summary>
/// <remarks>
///     The index and the rows are one module because the two cannot be told apart from outside: <see cref="Scroll" />
///     finds what is picked by <see cref="Role.Selection" /> and the topmost thing by <see cref="Line.Item" />, so a
///     screen that stamped its own rows could break scrolling in a module it never touches, with nothing to catch it
///     at compile time (#51). No screen stamps a row itself — it says how one thing draws, and the gutter and the
///     ordinal are put on here.
///     <para>
///         Content warnings stay out. Only posts have one, three of the screens holding this have no posts on them at
///         all, and <see cref="Revealed" /> is already the thing that answers for them.
///     </para>
/// </remarks>
public sealed class Picked<T>(IReadOnlyList<T> things) : IPicked
{
    private readonly List<T> _things = [.. things];

    /// <summary>Which thing is picked out, as an index into what is on screen.</summary>
    public int At { get; private set; }

    /// <summary>The things, in the order they are drawn and walked.</summary>
    public IReadOnlyList<T> All => _things;

    /// <summary>How many there are.</summary>
    public int Count => _things.Count;

    /// <summary>
    ///     The thing picked out, or <see langword="null" /> where there are none — which is a fact about the list
    ///     rather than a place in it.
    /// </summary>
    /// <remarks>
    ///     Named for the second half of the phrase because a member cannot be called what its own type is called, and
    ///     the type is what carries the word: a screen reads <c>_posts.Out</c> as the post picked out of them.
    /// </remarks>
    public T? Out => _things.Count == 0 ? default : _things[At];

    /// <inheritdoc />
    /// <remarks>
    ///     Counted in <see cref="long" /> because <c>Home</c> and <c>End</c> ask to move by the largest step there is,
    ///     and adding that to an index overflows back to the other end of the list.
    /// </remarks>
    public void Move(int by)
    {
        if (_things.Count > 0)
        {
            At = (int)Math.Clamp((long)At + by, 0, _things.Count - 1);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     What <c>j</c> does once the arrows have scrolled what is picked off the page: a step from where the pick
    ///     was left would take the reader back to something they can no longer see (#51).
    /// </remarks>
    public void Pick(int at)
    {
        if (_things.Count > 0)
        {
            At = Math.Clamp(at, 0, _things.Count - 1);
        }
    }

    /// <summary>
    ///     Puts <paramref name="thing" /> at the end of the list, which is where a message this profile has just sent
    ///     belongs in the thread it answers.
    /// </summary>
    public void Add(T thing) => _things.Add(thing);

    /// <summary>
    ///     Walks the list once, putting back whatever <paramref name="changed" /> makes of each thing — which for most
    ///     of them is the thing itself.
    /// </summary>
    /// <remarks>
    ///     Said here because the four screens that rewrite what they hold rewrite it for four different reasons — a
    ///     mark put on a post, a conversation read, a follow answered, a post taken down — and four walks of the same
    ///     list would be four chances to walk it differently. The pick cannot move: a list rewritten is a list of the
    ///     same length, in the same order.
    /// </remarks>
    public void Rewrite(Func<T, T> changed)
    {
        for (var at = 0; at < _things.Count; at++)
        {
            _things[at] = changed(_things[at]);
        }
    }

    /// <summary>
    ///     Takes every thing <paramref name="which" /> picks out off the list, and brings the pick back inside what is
    ///     left.
    /// </summary>
    /// <remarks>
    ///     The re-clamp is the point of asking here rather than of the list: rows are worked out afresh every frame, so
    ///     an index past the end of the list is a screen with nothing picked out and no way to say so.
    /// </remarks>
    public void Remove(Func<T, bool> which)
    {
        _things.RemoveAll(thing => which(thing));

        At = _things.Count == 0 ? 0 : Math.Clamp(At, 0, _things.Count - 1);
    }

    /// <summary>
    ///     The things as rows, each behind a gutter that says whether it is the one picked out and each naming the
    ///     thing it belongs to, with a blank between them.
    /// </summary>
    /// <remarks>
    ///     The blank between two things belongs to neither, so a page that begins on one begins on the thing under it.
    /// </remarks>
    /// <param name="width">How wide the content region is — 61 at an 80-column terminal.</param>
    /// <param name="draw">How one thing draws itself.</param>
    public IReadOnlyList<Line> Rows(int width, Draws<T> draw)
    {
        var lines = new List<Line>();

        for (var at = 0; at < _things.Count; at++)
        {
            lines.AddRange(RowsOf(at, width, draw));
            lines.Add(Line.Blank);
        }

        return lines;
    }

    /// <summary>The <paramref name="at" />th thing's rows on their own, stamped the same way and with no blank after.</summary>
    /// <remarks>
    ///     For the two screens that put something of their own between the things — search, whose three kinds each get
    ///     a heading, and the post screen, which says how many replies follow the post itself. Splicing rows between
    ///     things is theirs; stamping the things' own rows is still not.
    /// </remarks>
    public IReadOnlyList<Line> RowsOf(int at, int width, Draws<T> draw)
    {
        var gutter = Gutter(at == At);

        return [.. draw(_things[at], at, Math.Max(1, width - 1)).Select(line => line.After(gutter).PartOf(at))];
    }

    /// <summary>
    ///     The one column that says which row is picked out, by a mark as well as by a role. Always taken, so that
    ///     moving the pick down does not shift every thing sideways as it goes.
    /// </summary>
    private static Span Gutter(bool picked) => new(picked ? "▌" : " ", picked ? Role.Selection : Role.Body);
}
