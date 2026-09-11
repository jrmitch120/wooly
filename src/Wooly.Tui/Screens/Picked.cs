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
    /// <summary>
    ///     Which thing is picked out, as an index into what is on screen — the same ordinal the rows are stamped with,
    ///     so a screen can ask whether the thing it is drawing is the one being read (<see cref="Screen.ReferenceOn" />).
    /// </summary>
    int At { get; }

    /// <summary>
    ///     How many things there are to walk, which is nought on a screen with nothing on it — an inbox with nothing
    ///     waiting, a search that found nothing.
    /// </summary>
    /// <remarks>
    ///     Asked of the walk rather than of each screen, so that whether a screen is empty is one question with one
    ///     answer: <see cref="Screen.Keys" /> takes a screen's own keys off an empty row by it (#195), and a screen
    ///     added later inherits that without knowing it exists. Distinct from having nothing to walk <em>at all</em>,
    ///     which is having none of this rather than a count of nought — a compose editor is not an empty list.
    /// </remarks>
    int Count { get; }

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

    /// <inheritdoc />
    public int At { get; private set; }

    /// <summary>The things, in the order they are drawn and walked.</summary>
    public IReadOnlyList<T> All => _things;

    /// <inheritdoc />
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
    ///     thing it belongs to, with a <see cref="Line.Rule" /> between them.
    /// </summary>
    /// <remarks>
    ///     A rule rather than the blank row this used to be — variant F of the six put on screen for #62. A feed of
    ///     short posts read as one undifferentiated column of text, and finding where a post ended was work the reader
    ///     did rather than work the screen did; the rule costs the same one row the blank did.
    ///     <para>
    ///         The rule between two things belongs to neither, so a page that begins on one begins on the thing under
    ///         it — and being a row of no thing is also what keeps it clear of <see cref="Role.Selection" />'s
    ///         <c>▌</c>, which is only ever stamped on a thing's own rows.
    ///     </para>
    /// </remarks>
    /// <param name="width">How wide the content region is — 61 at an 80-column terminal.</param>
    /// <param name="draw">How one thing draws itself.</param>
    /// <param name="ordinals">
    ///     Where these things stand in the numbering of the screen holding them, which is their own numbering on every
    ///     screen but one (<see cref="Ordinals" />).
    /// </param>
    public IReadOnlyList<Line> Rows(int width, Draws<T> draw, Ordinals ordinals = default) =>
        Rows(width, draw, ordinals, from: 0, howMany: Count);

    /// <inheritdoc cref="Rows(int, Draws{T}, Ordinals)" />
    /// <summary>
    ///     The same for one run of them: <paramref name="howMany" /> things from <paramref name="from" />, stamped and
    ///     ruled exactly as they are when the whole list is drawn at once.
    /// </summary>
    /// <remarks>
    ///     For a screen that splices a heading into the middle of one list — the account screen, whose pinned posts and
    ///     timeline are one list under two headings (#182). Here rather than in that screen, so that there is one loop
    ///     putting a rule between two things rather than one per screen that ever splits a list in two.
    /// </remarks>
    /// <param name="from">Which of these things the run begins at, counted from the first of them.</param>
    /// <param name="howMany">How many of them are in it.</param>
    public IReadOnlyList<Line> Rows(int width, Draws<T> draw, Ordinals ordinals, int from, int howMany)
    {
        var lines = new List<Line>();

        for (var at = from; at < from + howMany; at++)
        {
            lines.AddRange(RowsOf(at, width, draw, ordinals));
            lines.Add(Line.Rule(width));
        }

        return lines;
    }

    /// <inheritdoc cref="Rows(int, Draws{T}, Ordinals)" />
    /// <summary>
    ///     The same with <paramref name="between" /> standing between the things rather than a rule after each of
    ///     them — for a list of people, where a rule apiece would be hundreds of rows of horizontal line and a blank
    ///     says the same thing in one (#180, #198).
    /// </summary>
    /// <remarks>
    ///     Between rather than after, so nothing is left hanging under the last thing — and here rather than at each
    ///     screen that wants it, for the reason the rule is here: a separator belongs to neither of the two things it
    ///     stands between, so no thing may draw its own.
    /// </remarks>
    /// <param name="between">The row to put between two things, drawn as many times as there are gaps.</param>
    public IReadOnlyList<Line> Rows(int width, Draws<T> draw, Line between, Ordinals ordinals = default)
    {
        var lines = new List<Line>();

        for (var at = 0; at < Count; at++)
        {
            if (at > 0)
            {
                lines.Add(between);
            }

            lines.AddRange(RowsOf(at, width, draw, ordinals));
        }

        return lines;
    }

    /// <inheritdoc cref="Rows(int, Draws{T}, Ordinals)" />
    /// <summary>The <paramref name="at" />th thing's rows on their own, stamped the same way and with no rule after.</summary>
    /// <remarks>
    ///     For the two screens that put something of their own between the things — search, whose three kinds each get
    ///     a heading, and the post screen, which says how many replies follow the post itself. Splicing rows between
    ///     things is theirs; stamping the things' own rows is still not.
    /// </remarks>
    /// <param name="at">Which of these things, counted from the first of them rather than from the screen's own first.</param>
    public IReadOnlyList<Line> RowsOf(int at, int width, Draws<T> draw, Ordinals ordinals = default)
    {
        var ordinal = ordinals.From + at;

        return Stamped.Rows(
            draw(_things[at], ordinal, Stamped.Room(width)),
            ordinal,
            ordinal == (ordinals.Picked ?? ordinals.From + At));
    }
}

/// <summary>
///     Where a list's things stand in the numbering of the screen holding them: the ordinal its first thing takes, and
///     which ordinal the screen has picked out.
/// </summary>
/// <remarks>
///     Nought and the list's own pick everywhere but the account screen, whose first walkable thing is the header
///     block rather than a post — so its posts are numbered from one, and while the header is picked nothing on the
///     list is (#179, ADR-0019). Said as one thing rather than two parameters because the two are one fact: a list
///     that is not the whole of what its screen walks has neither the screen's numbering nor the screen's pick.
///     <para>
///         The default is a screen that walks nothing else, which is every other screen — so the parameter is passed
///         only where it says something.
///     </para>
/// </remarks>
/// <param name="From">The ordinal the first thing on the list takes.</param>
/// <param name="Picked">
///     Which ordinal is picked out, or <see langword="null" /> where the list's own pick is the screen's — which is
///     what makes the default the behaviour every screen had before there was anything else to pick.
/// </param>
public readonly record struct Ordinals(int From = 0, int? Picked = null);

/// <summary>
///     One thing on a screen as rows: behind the one column that says whether it is the thing picked out, and each row
///     naming the thing it belongs to.
/// </summary>
/// <remarks>
///     Said here rather than by whatever drew the rows, for the reason <see cref="Picked{T}" /> gives about stamping:
///     <see cref="Scroll" /> finds what is picked by <see cref="Role.Selection" /> and the topmost thing by
///     <see cref="Line.Item" />, so a screen marking its own rows could break scrolling in a module it never touches.
///     A list stamps every thing on it this way, and the account screen's header block — a thing on a screen that is on
///     no list — takes the same treatment through the same call rather than a second gutter of its own (#179).
/// </remarks>
internal static class Stamped
{
    /// <summary>
    ///     How much room a thing's own rows get: the content region less the one column its gutter takes, and never
    ///     nothing at all.
    /// </summary>
    /// <remarks>
    ///     Beside the stamping rather than worked out where each thing is drawn, because the column and the mark put in
    ///     it are one fact: a caller narrowing by a different amount would draw rows the gutter then pushed past the
    ///     right of the region.
    /// </remarks>
    /// <param name="width">How wide the content region is — 61 at an 80-column terminal.</param>
    public static int Room(int width) => Math.Max(1, width - 1);

    /// <inheritdoc cref="Stamped" />
    /// <param name="lines">The rows the thing itself draws, in the room its gutter has already left it.</param>
    /// <param name="at">Which thing on the screen it is, in the screen's own numbering.</param>
    /// <param name="picked">Whether it is the one picked out.</param>
    public static IReadOnlyList<Line> Rows(IReadOnlyList<Line> lines, int at, bool picked)
    {
        var gutter = Gutter(picked);

        return [.. lines.Select(line => line.After(gutter).PartOf(at))];
    }

    /// <summary>
    ///     The one column that says which row is picked out, by a mark as well as by a role. Always taken, so that
    ///     moving the pick down does not shift every thing sideways as it goes.
    /// </summary>
    private static Span Gutter(bool picked) => new(picked ? "▌" : " ", picked ? Role.Selection : Role.Body);
}
