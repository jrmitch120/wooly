using System.Text;
using Wooly.Tui.Media;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Rendering;

/// <summary>One row of a screen: the spans across it, left to right.</summary>
public sealed record Line
{
    /// <summary>A row with nothing on it, which is a row of the screen and not an absence of one.</summary>
    public static readonly Line Blank = new([]);

    /// <param name="spans">The runs across the row, left to right.</param>
    public Line(IReadOnlyList<Span> spans) => Spans = spans;

    /// <summary>The runs across the row, left to right.</summary>
    public IReadOnlyList<Span> Spans { get; }

    /// <summary>
    ///     The pictures whose boxes start on this row, and nothing on the rows they go on to cover. A band is named
    ///     once, at its top, so that a view has a single place to read a box's whole shape from — including when the
    ///     top of it has been scrolled off and only its lower rows are still on screen.
    /// </summary>
    public IReadOnlyList<Inset> Insets { get; init; } = [];

    /// <summary>
    ///     The picture this row stands in for, where it is one a terminal could draw — set whether or not the pixels
    ///     have arrived, which is the point of it.
    /// </summary>
    /// <remarks>
    ///     What a post is made of does not depend on where the reader has scrolled to, so every post in a list works
    ///     out its rows whether or not it is anywhere near the screen. Sending for a picture from there would send for
    ///     an account's whole gallery to draw the four of it that fit. So the rows only say which pictures they
    ///     <em>want</em>, and <see cref="Media.Placing" />, handed the scroll by the view that knows where it has got
    ///     to, decides which of those are worth asking for (ADR-0016).
    /// </remarks>
    public Drawn? Wants { get; init; }

    /// <summary>
    ///     The pictures this row stands in for after <see cref="Wants" />, left to right, where it holds several boxes
    ///     side by side: a notification group's avatar strip (#454). Wanted on the same terms as <see cref="Wants" />.
    /// </summary>
    /// <remarks>
    ///     A second mark rather than <see cref="Wants" /> made a list, because every other row stands in for one
    ///     picture at most, and saying so is what <see cref="Wants" /> already does.
    /// </remarks>
    public IReadOnlyList<Drawn> AlsoWants { get; init; } = [];

    /// <summary>Every picture this row stands in for, left to right: <see cref="Wants" />, then <see cref="AlsoWants" />.</summary>
    public IReadOnlyList<Drawn> Wanted => Wants is { } first ? [first, .. AlsoWants] : AlsoWants;

    /// <summary>
    ///     Pictures a keypress would put on screen in place of what is there now, each in the box it would be set in
    ///     and with how far under this row that is, which are not drawn — the Picture screen's other pictures whose
    ///     originals have landed, for one, which <c>←</c> or <c>→</c> steps to (review of #514). Encoded ahead by
    ///     <see cref="Media.Placing" /> as a picture near the page is, so that the keypress draws one on its own frame
    ///     rather than a frame or an encode behind it.
    /// </summary>
    public IReadOnlyList<InsetAhead> Ahead { get; init; } = [];

    /// <summary>
    ///     Which of the screen's things this row is part of — the same ordinal <see cref="Screens.Screen.Pick" />
    ///     takes — or <see langword="null" /> for a row that is part of none: a heading, a notice, the blank between
    ///     two posts.
    /// </summary>
    /// <remarks>
    ///     What answers "which post is this row" once the reader has scrolled away from the one they picked.
    ///     <see cref="Role.Selection" /> cannot: by definition it marks the one post already selected, so a page it
    ///     has scrolled off is a page on which nothing says what is on it.
    /// </remarks>
    public int? Item { get; init; }

    /// <summary>
    ///     Whether this row heads the run of things under it — <c>── 7 accounts ──</c> over the accounts a search
    ///     found — which is what <c>[</c> and <c>]</c> move between and what a jump brings onto the page with the
    ///     thing it lands on.
    /// </summary>
    /// <remarks>
    ///     Marked here, beside <see cref="Item" />, for the reason <see cref="Item" /> is: <see cref="Scroll" /> and
    ///     <see cref="Sections" /> answer from the rows alone, so the runs a reader can see are the runs the key moves
    ///     between by construction — rather than a screen keeping a second count of its own sections in step with the
    ///     headings it draws (#51, #166).
    ///     <para>
    ///         The row itself is part of no thing: a heading belongs to the run below it rather than to anything in
    ///         it, which is why this is a mark of its own and not a value of <see cref="Item" />.
    ///     </para>
    /// </remarks>
    public bool Heads { get; init; }

    /// <summary>
    ///     Whether this row is where its thing's content starts — the first row of a post after its byline and avatar,
    ///     the warning, the text or whatever is attached, whichever is drawn first — which is where
    ///     <see cref="Scroll" /> counts the thing as beginning when it reclaims one off the page (asked in review of
    ///     #480).
    /// </summary>
    /// <remarks>
    ///     Marked where a post's rows are built, for the reason <see cref="Heads" /> is: read off the rows as drawn, no
    ///     screen keeps its own record of where a thing's content starts. A thing with no row so marked — an account's
    ///     header block, a follow — begins at its first row.
    /// </remarks>
    public bool ContentStarts { get; init; }

    /// <summary>
    ///     Whether this row is part of the thing picked out, and so drawn on <see cref="Theme.Role.Band" /> from one
    ///     edge of the view to the other rather than on the page (#269).
    /// </summary>
    /// <remarks>
    ///     A mark on the row rather than a role swapped onto each of its spans, because what a row is is decided where
    ///     it is built — <see cref="Screens.Stamped" /> — and the view that paints it only answers the mark. A span
    ///     with a background of its own keeps it, so a picked reference still says it is picked on a banded row.
    /// </remarks>
    public bool Picked { get; init; }

    /// <summary>
    ///     Where the band of a picked row stops, counted in columns from the row's start, or <see langword="null" /> where
    ///     it runs to the edge of the view: a row that is two things side by side — the file browser's list and its
    ///     preview pane — picks out only the first.
    /// </summary>
    public int? BandsTo { get; init; }

    /// <summary>
    ///     Whether column <paramref name="column" /> of this row is on the band rather than the page: on a
    ///     <see cref="Picked" /> row, short of where <see cref="BandsTo" /> stops it. What a picture's box on this row
    ///     shows through its transparent pixels, on Kitty's placeholders and sixel alike.
    /// </summary>
    public bool BandedAt(int column) => Picked && column < (BandsTo ?? int.MaxValue);

    /// <summary>
    ///     Whether this row is a rule (<see cref="Rule" />): its one span repeated by the view that paints it from the
    ///     row's first column to the view's last, rather than drawn as wide as it is.
    /// </summary>
    public bool Ruled { get; init; }

    /// <summary>What the row reads as with the roles taken off — what a test asserts against, and what a screenshot shows.</summary>
    public string Text
    {
        get
        {
            var text = new StringBuilder();

            foreach (var span in Spans)
            {
                text.Append(span.Text);
            }

            return text.ToString();
        }
    }

    /// <summary>How wide the row is.</summary>
    public int Width => Spans.Sum(span => span.Width);

    /// <summary>A row of the given spans.</summary>
    public static Line Of(params Span[] spans) => new(spans);

    /// <summary>A row of one span, which is most of them.</summary>
    public static Line Of(string text, Role role) => new([new Span(text, role)]);

    /// <summary>
    ///     A row ruled across the whole width of the view it is painted in, which is what says one thing on a screen
    ///     has ended and the next begun.
    /// </summary>
    /// <remarks>
    ///     Said here rather than by whatever puts one between two things, so that the separator a list draws and the
    ///     separator a screen splicing headings between things draws cannot come to be two different separators
    ///     (<see cref="Screens.Picked{T}.Rows" />).
    ///     <para>
    ///         It carries no width: its one span is the stroke, which the view repeats to its edge the way it runs a
    ///         picked row's band out (#269), so a rule reaches the edge whatever width the screen was drawn at (#494).
    ///     </para>
    /// </remarks>
    public static Line Rule() => Of("─", Theme.Role.Muted) with { Ruled = true };

    /// <summary>
    ///     The role every span on this row takes, or <see langword="null" /> where they do not agree. What a test asks
    ///     when the question is about the row rather than about part of it.
    /// </summary>
    public Role? Role => Spans.Count > 0 && Spans.All(span => span.Role == Spans[0].Role) ? Spans[0].Role : null;

    /// <summary>Whether any span on this row takes <paramref name="role" />.</summary>
    public bool Has(Role role) => Spans.Any(span => span.Role == role);

    /// <summary>
    ///     Whether any span on this row is drawn for <paramref name="reference" /> (<see cref="Span.Reference" />): a
    ///     bracket, its text, or a row of it the wrap carried on to.
    /// </summary>
    public bool Has(Reference reference) => Spans.Any(span => span.Reference == reference);

    /// <summary>This row, said to be part of the <paramref name="item" />th thing on the screen.</summary>
    public Line PartOf(int item) => this with { Item = item };

    /// <summary>This row, said to head the run of things under it.</summary>
    public Line Heading() => this with { Heads = true };

    /// <summary>This row, said to be where its thing's content starts.</summary>
    public Line StartingContent() => this with { ContentStarts = true };

    /// <summary>This row with <paramref name="spans" /> put in front of it.</summary>
    /// <remarks>
    ///     Anything put in front moves the rest of the row along, so a picture's box moves with it. A gutter added to a
    ///     row and a picture drawn where the gutter used to be is what this exists to prevent.
    /// </remarks>
    public Line After(params Span[] spans)
    {
        var shift = spans.Sum(span => span.Width);

        return Respanned([.. spans, .. Spans]) with
        {
            Insets = Insets.Count == 0 ? Insets : [.. Insets.Select(inset => inset.ShiftedBy(shift))],
            Ahead = Ahead.Count == 0 ? Ahead : [.. Ahead.Select(ahead => ahead.ShiftedBy(shift))],
            BandsTo = BandsTo + shift,
        };
    }

    /// <summary>
    ///     This row standing in for <paramref name="pictures" />, left to right, in place of what it stood in for: the
    ///     first its <see cref="Wants" /> and the rest its <see cref="AlsoWants" /> — so that a row adding pictures to
    ///     the ones it already wants says only which come first (<see cref="Wanted" />).
    /// </summary>
    public Line Wanting(IReadOnlyList<Drawn> pictures) => this with
    {
        Wants = pictures.Count > 0 ? pictures[0] : null,
        AlsoWants = [.. pictures.Skip(1)],
    };

    /// <summary>This row with <paramref name="spans" /> put after it, everything already on it kept where it is.</summary>
    public Line Then(params Span[] spans) => Respanned([.. Spans, .. spans]);

    /// <summary>
    ///     This row with <paramref name="spans" /> in place of its own, and everything else about it kept — the one place
    ///     a row's marks are carried across, so a mark added to a row cannot be dropped by one of the things that
    ///     rebuild one.
    /// </summary>
    public Line Respanned(IReadOnlyList<Span> spans) => new(spans)
    {
        Insets = Insets,
        Wants = Wants,
        AlsoWants = AlsoWants,
        Ahead = Ahead,
        Item = Item,
        Heads = Heads,
        ContentStarts = ContentStarts,
        Picked = Picked,
        BandsTo = BandsTo,
        Ruled = Ruled,
    };
}
