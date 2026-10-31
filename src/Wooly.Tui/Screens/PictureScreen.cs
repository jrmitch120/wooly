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
///     Drawn at the instance's original (#517), and at the smaller copy the timeline already holds until the original
///     lands, stretched to the box (#516), so that something is there the moment it opens; its <b>Stand-in</b> stands
///     where neither is here. The box is settled from the original's shape, so the two fill the same cells and the
///     original replaces the smaller copy without anything moving; and afresh on every frame from the room the window
///     has, so a resize draws the picture to fit the new one. An original that will not come leaves the smaller copy
///     standing, and the status row saying why.
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
/// <param name="post">The post the picture is on, which may be a boost: its author is who the crumb names.</param>
/// <param name="picture">The picture opened, which is the one shown until <c>←</c> or <c>→</c> steps off it.</param>
public sealed class PictureScreen(Post post, PostMedia picture) : Screen
{
    /// <summary>
    ///     What <c>←</c> and <c>→</c> step through: the post's pictures, or just the one opened where it is not among
    ///     them.
    /// </summary>
    private readonly List<PostMedia> _pictures = PicturesOn(post) is var on && on.Contains(picture) ? on : [picture];

    /// <summary>Which of <see cref="_pictures" /> is showing, the one opened until a step.</summary>
    private int _showing = Math.Max(0, PicturesOn(post).IndexOf(picture));

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
    public override string Crumb => $"Picture by @{(post.Boosted ?? post).Account}";

    /// <summary>
    ///     How this screen puts a word on the status row, or takes its word off it: why the original it draws would
    ///     not come. Given it by the shell that pushes it, which is the one that owns the row.
    /// </summary>
    internal Action<string?>? Saying { get; set; }

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
        [new("esc", "back"), .. Several ? [new KeyHint("←→", "picture")] : Array.Empty<KeyHint>()];

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
        var smaller = Drawn.Attached(Showing);
        var drawn = drawing.Pictures?.Of(original) is null ? smaller : original;
        var box = PictureBox.Shaped(drawn, Showing.OriginalShape ?? Showing.Shape, Showing.Blurhash, drawing, room);

        Tell(original, drawing.Pictures?.WhyNot(original));

        return [.. box is null ? [] : Boxed(box, original, smaller, drawing.Width), .. described, Hint];
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
            new Span("esc", Role.Key),
            new Span(" back · ", Role.Muted),
            new Span("←→", Role.Key),
            new Span($" picture {_showing + 1} of {_pictures.Count}", Role.Muted))
        : Line.Of(new Span("esc", Role.Key), new Span(" back", Role.Muted));

    /// <summary>
    ///     <paramref name="box" />'s rows, centred in <paramref name="width" /> — its top row saying it waits for
    ///     <paramref name="original" />, there being no caption above it to say so, and for <paramref name="smaller" />
    ///     beside it, which stands in until the original lands and is the one drawn where it never does.
    /// </summary>
    private static IEnumerable<Line> Boxed(PictureBox box, Drawn original, Drawn smaller, int width)
    {
        var column = Math.Max(0, (width - box.Columns) / 2);
        var before = new Span(new string(' ', column), Role.Body);

        for (var row = 0; row < box.Rows; row++)
        {
            var line = Line.Of(before, box.Cells(row)) with { Insets = box.Insets(row, column) };

            yield return row == 0 ? line with { Wants = original, AlsoWants = [smaller] } : line;
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
            Saying?.Invoke(Said(refused));
        }
        else if (hadSaid)
        {
            Saying?.Invoke(null);
        }
    }

    /// <summary>Why the smaller copy is still what is drawn, in words.</summary>
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
    ///     post is passed over, its preview being no picture of its own.
    /// </summary>
    private static List<PostMedia> PicturesOn(Post post) =>
        [.. (post.Boosted ?? post).Media.Where(media => media.Kind is MediaKind.Image)];
}
