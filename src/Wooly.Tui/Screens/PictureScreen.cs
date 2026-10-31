using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One still picture on a post over the whole window, its description beneath it and a hint row under that — what
///     <c>⏎</c> on a walked picture opens onto, and where a screenshot of small text is read (GLOSSARY.md's
///     <b>Picture screen</b>, ADR-0034).
/// </summary>
/// <remarks>
///     Drawn at the instance's original (#517), its <b>Stand-in</b> standing in the box until the original lands: not
///     the smaller copy the timeline already holds, which drawn first and then swapped for the original read as the
///     picture flashing (review of #514). The box is settled from the original's shape, so the original fills the
///     Stand-in's cells without anything moving; and afresh on every frame from the room the window has, so a resize
///     draws the picture to fit the new one. Only an original that will not come — too large, or not fetched or not
///     read — has the smaller copy drawn in its place, stretched to the box, and the status row saying why.
///     <para>
///         The original is held apart from the timeline's pictures, and only while a frame of this screen wants it
///         (<see cref="Drawn.HeldApart" />): the first frame drawn without it — the screen gone, or another picture
///         on it — lets it go.
///     </para>
///     <para>
///         Nothing on it is walked, and nothing is picked: the first press of <c>esc</c> goes back, to the screen
///         underneath with its pick and its walked picture as they were — which is why opening the picture is the one
///         opening that leaves its reference walked (<see cref="Shell.Shell.OpenReference" />).
///     </para>
///     <para>
///         <c>←</c> and <c>→</c> step along the post's other still pictures in the order they are drawn, stopping at
///         either end (#518): verbs of the screen's own rather than a walk of references, which <c>esc</c> would let go
///         of before going back, and kept here alone, so that what is walked underneath is the picture it was opened on,
///         whichever was stepped to.
///     </para>
/// </remarks>
public sealed class PictureScreen : Screen
{
    /// <summary>The key that goes back, and what it is said to do, on the status row and the hint row alike.</summary>
    private static readonly KeyHint Back = new("esc", "back");

    /// <summary>The keys that step, and what they are said to do, on the status row and the hint row alike.</summary>
    private static readonly KeyHint Step = new("←→", "picture");

    /// <summary>The post the picture is on, which may be a boost.</summary>
    private readonly Post _post;

    /// <summary>How this screen puts a word on the status row, or takes its word off it.</summary>
    private readonly Action<string?> _saying;

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
    /// <param name="saying">
    ///     How this screen puts a word on the status row, or takes its word off it: why the original it draws would
    ///     not come. Given it by the shell that pushes it, which is the one that owns the row.
    /// </param>
    public PictureScreen(Post post, PostMedia picture, Action<string?> saying)
    {
        var on = PicturesOn(post);

        _post = post;
        _saying = saying;
        _pictures = on.Contains(picture) ? on : [picture];
        _showing = _pictures.IndexOf(picture);
    }

    /// <summary>
    ///     The picture this screen draws, the one opened or the one stepped to since — what the box, the description
    ///     and what the box waits for are all read off.
    /// </summary>
    public PostMedia Showing => _pictures[_showing];

    /// <summary>
    ///     The original this screen last said something about, and what it said: why that original would not come, or
    ///     nothing where it was coming or here. What keeps a refusal said once rather than on every frame.
    /// </summary>
    private (string Id, Unshowable? Why)? _told;

    /// <inheritdoc />
    public override string Crumb => $"Picture by @{(_post.Boosted ?? _post).Account}";

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
    ///     The box first, as large as the window less the description's rows and the hint row allows, fitted whole at
    ///     the picture's shape and centred across the width; then the description, wrapped rather than cut, since here
    ///     it is all there is to read beside the picture; then the hint row. Where nobody said how tall the window is,
    ///     the box is capped as a post screen's is (<see cref="Inset.WholeRows" />).
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var described = Described(drawing.Width);
        var room = drawing.Height is { } height ? height - described.Count - 1 : Inset.WholeRows;
        var original = Drawn.Original(Showing, drawing.Width, room);
        var why = drawing.Pictures?.WhyNot(original);
        Drawn? fallback = why is null ? null : Drawn.Attached(Showing);
        var box = PictureBox.Shaped(
            fallback ?? original,
            Showing.OriginalShape ?? Showing.Shape,
            Showing.Blurhash,
            drawing,
            room);

        Tell(original, why);

        return [.. box is null ? [] : Boxed(box, original, fallback, drawing.Width), .. described, Hint];
    }

    /// <summary>Whether the post has more than the one picture, and so anything for <c>←</c> and <c>→</c> to reach.</summary>
    private bool Several => _pictures.Count > 1;

    /// <summary>
    ///     The keys this screen answers to, said under the picture as well as on the status row, since the picture is the
    ///     whole of the screen and the row is easy to miss beneath it — and where there are several, which of how many
    ///     this is.
    /// </summary>
    private Line Hint => Several
        ? Line.Of(
            new Span(Back.Key, Role.Key),
            new Span($" {Back.Does} · ", Role.Muted),
            new Span(Step.Key, Role.Key),
            new Span($" {Step.Does} {_showing + 1} of {_pictures.Count}", Role.Muted))
        : Line.Of(new Span(Back.Key, Role.Key), new Span($" {Back.Does}", Role.Muted));

    /// <summary>
    ///     <paramref name="box" />'s rows, centred in <paramref name="width" /> — its top row saying it waits for
    ///     <paramref name="original" />, there being no caption above it to say so, and for <paramref name="fallback" />
    ///     beside it where there is one: the smaller copy, drawn only where the original will not come.
    /// </summary>
    /// <remarks>
    ///     The original is still wanted while the smaller copy stands in for it, so that what was learnt of it stays
    ///     held and it is not sent for again on the next frame.
    /// </remarks>
    private static IEnumerable<Line> Boxed(PictureBox box, Drawn original, Drawn? fallback, int width)
    {
        var column = Math.Max(0, (width - box.Columns) / 2);
        var before = new Span(new string(' ', column), Role.Body);

        for (var row = 0; row < box.Rows; row++)
        {
            var line = Line.Of(before, box.Cells(row)) with { Insets = box.Insets(row, column) };

            yield return row == 0
                ? line with { Wants = original, AlsoWants = fallback is null ? [] : [fallback] }
                : line;
        }
    }

    /// <summary>
    ///     Says on the status row why <paramref name="original" /> will not come, the first frame that learns it — or,
    ///     where it is coming after all and this screen had said otherwise of the one before it, takes that word off.
    /// </summary>
    private void Tell(Drawn original, Unshowable? why)
    {
        if (_told == (original.Id, why))
        {
            return;
        }

        var hadSaid = _told?.Why is not null;

        _told = (original.Id, why);

        if (why is { } refused)
        {
            _saying(Said(refused));
        }
        else if (hadSaid)
        {
            _saying(null);
        }
    }

    /// <summary>Why the smaller copy is drawn in the original's place, in words.</summary>
    private static string Said(Unshowable why) => why is Unshowable.TooLarge
        ? "The original is too large to show."
        : "The original could not be fetched.";

    /// <summary>What its author said the picture shows, wrapped to <paramref name="width" />, or that they said nothing.</summary>
    private List<Line> Described(int width)
    {
        var role = Showing.Description is null ? Role.Muted : Role.Body;

        return [.. TextWrap.Wrap(Showing.Shows, width).Select(row => Line.Of(row, role))];
    }

    /// <summary>
    ///     <paramref name="post" />'s still pictures in the order they are drawn, which is the order they are stepped
    ///     through: every one is drawn where this screen could be opened at all, and a video or the like on the same
    ///     post is passed over, its preview being no picture of its own — the rule the walk keeps
    ///     (<see cref="AttachmentReferences.IsPicture" />).
    /// </summary>
    private static List<PostMedia> PicturesOn(Post post) =>
        [.. (post.Boosted ?? post).Media.Where(AttachmentReferences.IsPicture)];
}
