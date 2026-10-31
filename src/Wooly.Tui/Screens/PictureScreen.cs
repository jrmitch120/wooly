using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One still picture on a post over the whole window, its description centred beneath it — what <c>⏎</c> on a
///     walked picture opens onto, and where a screenshot of small text is read (GLOSSARY.md's
///     <b>Picture screen</b>, ADR-0034).
/// </summary>
/// <remarks>
///     Drawn at the instance's original (#517), the box left empty until the original lands — blank cells, with no
///     <b>Stand-in</b>'s shade or blur, a blur on a screen that shows the one picture having felt off — and not the
///     smaller copy the timeline already holds, which drawn first and then swapped for the original read as the
///     picture flashing (review of #514). The box is settled from the original's shape, so the original fills the
///     empty box's cells without anything moving; and afresh on every frame from the room the window has, so a resize
///     draws the picture to fit the new one. Only an original that will not come — too large, or not fetched or not
///     read — has the smaller copy drawn in its place, stretched to the box, and why said on a row of the screen's own
///     under the description, kept for it whether anything is said there or not so that nothing moves when it is.
///     <para>
///         Laid out from the drawing alone, every frame, with nothing kept from one frame to the next but which
///         picture is showing: what the screen wants of the cache, and what it says of an original that will not come,
///         are read off what the cache holds as of the frame (review of #514).
///     </para>
///     <para>
///         The original is held apart from the timeline's pictures, and only while a frame of this screen wants it
///         (<see cref="Drawn.HeldApart" />). It wants the one shown first, and the originals of all the post's other
///         pictures from the first frame one of them has landed or will not come — the one shown, that being the only
///         one sent for until then — so that the picture being looked at is not kept waiting on the ones that are not;
///         and from then until the screen goes, the one landed being held for as long as it is wanted, so that a step
///         to one already landed, or back to one stepped off, draws it at once rather than an empty box. The first
///         frame drawn without them, the screen gone, lets every one of them go.
///     </para>
///     <para>
///         Nothing on it is walked, and nothing is picked: the first press of <c>esc</c> goes back, to the screen
///         underneath with its pick as it was and nothing walked, opening the picture having let go of its reference as
///         opening any reference does (<see cref="Shell.Shell.OpenReference" />, #474, review of #514).
///     </para>
///     <para>
///         <c>←</c> and <c>→</c> step along the post's other still pictures in the order they are drawn, stopping at
///         either end (#518): verbs of the screen's own rather than a walk of references, which <c>esc</c> would let go
///         of before going back, stepping from the picture it was opened on, which it holds itself rather than reading
///         off a walk underneath.
///     </para>
/// </remarks>
public sealed class PictureScreen : Screen
{
    /// <summary>The key that goes back, and what it is said to do on the status row.</summary>
    private static readonly KeyHint Back = new("esc", "back");

    /// <summary>The keys that step, and what they are said to do on the status row.</summary>
    private static readonly KeyHint Step = new("←→", "picture");

    /// <summary>
    ///     How many rows under the description are kept for saying why an original will not come, whether anything is
    ///     said there or not.
    /// </summary>
    private const int WhyRows = 1;

    /// <summary>The post the picture is on, which may be a boost.</summary>
    private readonly Post _post;

    /// <summary>
    ///     What <c>←</c> and <c>→</c> step through: the post's pictures, or just the one opened where it is not among
    ///     them.
    /// </summary>
    private readonly List<PostMedia> _pictures;

    /// <summary>Which of <see cref="_pictures" /> is showing, the one opened until a step.</summary>
    private int _showing;

    /// <summary>A Picture screen opened on <paramref name="picture" />.</summary>
    /// <param name="post">The post the picture is on, which may be a boost: its author is who the crumb names.</param>
    /// <param name="picture">The picture opened, which is the one shown until <c>←</c> or <c>→</c> steps off it.</param>
    public PictureScreen(Post post, PostMedia picture)
    {
        var on = PicturesOn(post);

        _post = post;
        _pictures = on.Contains(picture) ? on : [picture];
        _showing = _pictures.IndexOf(picture);
    }

    /// <summary>
    ///     The picture this screen draws, the one opened or the one stepped to since — what the box, the description
    ///     and what the box waits for are all read off.
    /// </summary>
    public PostMedia Showing => _pictures[_showing];

    /// <inheritdoc />
    /// <remarks>
    ///     Counting the pictures where the post has several — <c>Picture 2 of 4 by Maria</c>, which of how many is
    ///     showing — since the status row says the keys that step but not where they have got to; and not where it has
    ///     the one, there being nothing to count.
    /// </remarks>
    public override string Crumb => Several
        ? $"Picture {_showing + 1} of {_pictures.Count} by {Author}"
        : $"Picture by {Author}";

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys => Several ? [Back, Step] : [Back];

    /// <inheritdoc />
    /// <remarks>
    ///     A step past either end changes nothing, as the walk does at its ends: there is no picture after the last for
    ///     <c>→</c> to come round to.
    /// </remarks>
    public override Task Answer(Verb verb, Reach reach)
    {
        var to = Math.Clamp(
            verb switch
            {
                Verb.NextPicture => _showing + 1,
                Verb.PreviousPicture => _showing - 1,
                _ => _showing,
            },
            0,
            _pictures.Count - 1);

        if (to != _showing)
        {
            _showing = to;
            reach.Changed();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The box first, as large as the window less the description's rows and the row kept for why allow, fitted
    ///     whole at the picture's shape and centred across the width; then the description, wrapped and centred under
    ///     it, since here it is all there is to read beside the picture, but cut at a quarter of the window
    ///     (<see cref="DescribedRows" />), so that the picture is given its room first; then why the original will not
    ///     come, where it will not, centred as the description is. Nothing under that: the status row says the keys,
    ///     and the crumb which picture of how many this is. Where nobody said how tall the window is, the box is capped
    ///     as a post screen's is (<see cref="Inset.WholeRows" />), and the description at a quarter of that.
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var laid = _pictures.Select(picture => Laid(picture, drawing)).ToList();
        var showing = laid[_showing];

        // The rest only once one of them has come — which until then can only be the one showing — and from then on.
        List<Layout> others = laid.Any(picture => picture.Come)
            ? [.. laid.Where((_, at) => at != _showing)]
            : [];

        return
        [
            .. showing.Box is { } box ? Boxed(box, showing, others, drawing.Width) : [],
            .. showing.Described,
            showing.Why is { } why ? Centred(Said(why), Role.Error, drawing.Width) : Line.Blank,
        ];
    }

    /// <summary>
    ///     Who the crumb names, the author of the post the picture is on rather than whoever boosted it, by their name as
    ///     one line of plain text holds it, as the post's own crumb names them (#525).
    /// </summary>
    private string Author => PlainName.OfAuthor(_post.Boosted ?? _post);

    /// <summary>
    ///     How <paramref name="picture" /> is laid out on this screen in <paramref name="drawing" />: its description,
    ///     and above it its original, sized to the room the description and the row kept for why leave, why that will
    ///     not come where it will not, the smaller copy drawn in its place then, and the box, empty until what it draws
    ///     has landed.
    /// </summary>
    private static Layout Laid(PostMedia picture, Drawing drawing)
    {
        var described = Described(picture, drawing);
        var room = drawing.Height is { } height ? height - described.Count - WhyRows : Inset.WholeRows;
        var original = Drawn.Original(picture, drawing.Width, room);
        var why = drawing.Pictures?.WhyNot(original);
        Drawn? fallback = why is null ? null : Drawn.Attached(picture);
        var box = PictureBox.ShapedBlankUntilItLands(
            fallback ?? original,
            picture.OriginalShape ?? picture.Shape,
            drawing,
            room);

        return new Layout(
            original,
            why,
            fallback,
            box,
            described,
            Come: why is not null || drawing.Pictures?.Of(original) is not null);
    }

    /// <summary>Whether the post has more than the one picture, and so anything for <c>←</c> and <c>→</c> to reach.</summary>
    private bool Several => _pictures.Count > 1;

    /// <summary>
    ///     <paramref name="box" />'s rows, centred in <paramref name="width" /> — its top row saying it waits for the
    ///     original of <paramref name="showing" />, there being no caption above it to say so, then for the smaller copy
    ///     where that is drawn, which is only where the original will not come, and for the originals of
    ///     <paramref name="others" />; and carrying, ahead, the pictures those of them that have landed are drawn in
    ///     once stepped to.
    /// </summary>
    /// <remarks>
    ///     The original is still wanted while the smaller copy stands in for it, so that what was learnt of it stays
    ///     held and it is not sent for again on the next frame.
    /// </remarks>
    private static IEnumerable<Line> Boxed(PictureBox box, Layout showing, List<Layout> others, int width)
    {
        var column = Indent(box.Columns, width);
        var before = new Span(new string(' ', column), Role.Body);

        for (var row = 0; row < box.Rows; row++)
        {
            var line = Line.Of(before, box.Cells(row)) with { Insets = box.Insets(row, column) };

            yield return row == 0
                ? line with
                {
                    Wants = showing.Original,
                    AlsoWants =
                    [
                        .. showing.Fallback is { } fallback ? [fallback] : Array.Empty<Drawn>(),
                        .. others.Select(other => other.Original),
                    ],
                    Ahead = [.. others.SelectMany(other => PicturesSetIn(other.Box, width))],
                }
                : line;
        }
    }

    /// <summary>
    ///     The picture <paramref name="box" /> sets into it where the box is centred in <paramref name="width" /> and
    ///     starts on the row carrying it, with how far under that row it is set — or nothing where there is no box or it
    ///     is empty, what it draws not having landed.
    /// </summary>
    private static IEnumerable<InsetAhead> PicturesSetIn(PictureBox? box, int width) =>
        box is null
            ? []
            : Enumerable.Range(0, box.Rows)
                        .SelectMany(row => box.Insets(row, Indent(box.Columns, width))
                                              .Select(inset => new InsetAhead(inset, Below: row)));

    /// <summary>
    ///     How far in something <paramref name="columns" /> wide starts where it is centred across
    ///     <paramref name="width" />: the box, and each row written under it.
    /// </summary>
    private static int Indent(int columns, int width) => Math.Max(0, (width - columns) / 2);

    /// <summary>
    ///     <paramref name="text" /> in <paramref name="role" />, centred across <paramref name="width" />.
    /// </summary>
    private static Line Centred(string text, Role role, int width) =>
        Line.Of(new Span(new string(' ', Indent(Glyphs.Columns(text), width)), Role.Body), new Span(text, role));

    /// <summary>
    ///     One picture as this screen lays it out: its original, why that will not come where it will not, the smaller
    ///     copy drawn in its place then, the box, the description under it, and whether its original has come — landed,
    ///     or will not come — which is what starts the post's other originals being sent for.
    /// </summary>
    private readonly record struct Layout(
        Drawn Original,
        Unshowable? Why,
        Drawn? Fallback,
        PictureBox? Box,
        List<Line> Described,
        bool Come);

    /// <summary>Why the smaller copy is drawn in the original's place, in words.</summary>
    private static string Said(Unshowable why) => why is Unshowable.TooLarge
        ? "The original is too large to show."
        : "The original could not be fetched.";

    /// <summary>
    ///     What its author said <paramref name="picture" /> shows, or that they said nothing, wrapped to the width of
    ///     <paramref name="drawing" />, each row centred in it as the picture above is, and cut to
    ///     <see cref="DescribedRows" /> of its height, the last row shown ending in <c>…</c> where it is cut.
    /// </summary>
    /// <remarks>
    ///     Shown as written, markup and all: a description is the author's plain text, and one that holds a whole
    ///     article's HTML — a post bridged from a blog can — is shown as that rather than read as a page.
    /// </remarks>
    private static List<Line> Described(PostMedia picture, Drawing drawing)
    {
        var width = drawing.Width;
        var mostRows = DescribedRows(drawing.Height ?? Inset.WholeRows);
        var role = picture.Description is null ? Role.Muted : Role.Body;
        var rows = TextWrap.Wrap(picture.Shows, width);
        IReadOnlyList<string> shown = rows.Count > mostRows
            ? [.. rows.Take(mostRows - 1), TextWrap.Clip($"{rows[mostRows - 1]}…", width)]
            : rows;

        return [.. shown.Select(row => Centred(row, role, width))];
    }

    /// <summary>
    ///     The most rows the description may take of <paramref name="height" />, the content region's: a quarter of
    ///     it, and never less than one — so that a description however long leaves the picture the rest, the picture
    ///     being what the screen is for.
    /// </summary>
    private static int DescribedRows(int height) => Math.Max(1, height / 4);

    /// <summary>
    ///     <paramref name="post" />'s still pictures in the order they are drawn, which is the order they are stepped
    ///     through: every one is drawn where this screen could be opened at all, and a video or the like on the same
    ///     post is passed over, its preview being no picture of its own — the rule the walk keeps
    ///     (<see cref="AttachmentReferences.IsPicture" />).
    /// </summary>
    private static List<PostMedia> PicturesOn(Post post) =>
        [.. (post.Boosted ?? post).Media.Where(AttachmentReferences.IsPicture)];
}
