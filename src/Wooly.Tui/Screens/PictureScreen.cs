using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
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
///         Nothing on it is walked, and nothing is picked: <c>esc</c> is the one key it says, and its first press goes
///         back, to the screen underneath with its pick and its walked picture as they were — which is why opening the
///         picture is the one opening that leaves its reference walked (<see cref="Shell.Shell.OpenReference" />).
///     </para>
/// </remarks>
/// <param name="post">The post the picture is on, which may be a boost: its author is who the crumb names.</param>
/// <param name="picture">The picture opened.</param>
public sealed class PictureScreen(Post post, PostMedia picture) : Screen
{
    /// <summary>The picture this screen draws.</summary>
    public PostMedia Picture => picture;

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
    protected override IReadOnlyList<KeyHint> OwnKeys => [new("esc", "back")];

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
        var original = Drawn.Original(picture, drawing.Width, room);
        var smaller = Drawn.Attached(picture);
        var drawn = drawing.Pictures?.Of(original) is null ? smaller : original;
        var box = PictureBox.Shaped(drawn, picture.OriginalShape ?? picture.Shape, picture.Blurhash, drawing, room);

        Tell(original, drawing.Pictures?.WhyNot(original));

        return [.. box is null ? [] : Boxed(box, original, smaller, drawing.Width), .. described, Hint];
    }

    /// <summary>
    ///     The keys this screen answers to, said under the picture as well as on the status row, since the picture is the
    ///     whole of the screen and the row is easy to miss beneath it.
    /// </summary>
    private static Line Hint => Line.Of(new Span("esc", Role.Key), new Span(" back", Role.Muted));

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
        var role = picture.Description is null ? Role.Muted : Role.Body;

        return [.. TextWrap.Wrap(picture.Shows, width).Select(row => Line.Of(row, role))];
    }
}
