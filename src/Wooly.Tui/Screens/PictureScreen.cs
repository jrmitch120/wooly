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
///     Drawn at the smaller copy the timeline already holds, stretched to the box (#516), so that something is there the
///     moment it opens; its <b>Stand-in</b> stands where that copy has been let go, until it is fetched again. The box is
///     settled afresh on every frame from the room the window has, so a resize draws the picture to fit the new one.
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

    /// <inheritdoc />
    public override string Crumb => $"Picture by @{(post.Boosted ?? post).Account}";

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
        var drawn = Drawn.Attached(picture);
        var box = PictureBox.Shaped(drawn, picture.Shape, picture.Blurhash, drawing, room);

        return [.. box is null ? [] : Boxed(box, drawn, drawing.Width), .. described, Hint];
    }

    /// <summary>
    ///     The keys this screen answers to, said under the picture as well as on the status row, since the picture is the
    ///     whole of the screen and the row is easy to miss beneath it.
    /// </summary>
    private static Line Hint => Line.Of(new Span("esc", Role.Key), new Span(" back", Role.Muted));

    /// <summary>
    ///     <paramref name="box" />'s rows, centred in <paramref name="width" /> — its top row saying it waits for
    ///     <paramref name="drawn" />, there being no caption above it to say so.
    /// </summary>
    private static IEnumerable<Line> Boxed(PictureBox box, Drawn drawn, int width)
    {
        var column = Math.Max(0, (width - box.Columns) / 2);
        var before = new Span(new string(' ', column), Role.Body);

        for (var row = 0; row < box.Rows; row++)
        {
            var line = Line.Of(before, box.Cells(row)) with { Insets = box.Insets(row, column) };

            yield return row == 0 ? line with { Wants = drawn } : line;
        }
    }

    /// <summary>What its author said the picture shows, wrapped to <paramref name="width" />, or that they said nothing.</summary>
    private List<Line> Described(int width)
    {
        var role = picture.Description is null ? Role.Muted : Role.Body;

        return [.. TextWrap.Wrap(picture.Shows, width).Select(row => Line.Of(row, role))];
    }
}
