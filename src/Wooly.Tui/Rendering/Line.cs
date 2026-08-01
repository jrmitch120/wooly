using System.Text;
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
    ///     The role every span on this row takes, or <see langword="null" /> where they do not agree. What a test asks
    ///     when the question is about the row rather than about part of it.
    /// </summary>
    public Role? Role => Spans.Count > 0 && Spans.All(span => span.Role == Spans[0].Role) ? Spans[0].Role : null;

    /// <summary>Whether any span on this row takes <paramref name="role" />.</summary>
    public bool Has(Role role) => Spans.Any(span => span.Role == role);

    /// <summary>This row with <paramref name="spans" /> put in front of it.</summary>
    /// <remarks>
    ///     Anything put in front moves the rest of the row along, so a picture's box moves with it. A gutter added to a
    ///     row and a picture drawn where the gutter used to be is what this exists to prevent.
    /// </remarks>
    public Line After(params Span[] spans)
    {
        var shift = spans.Sum(span => span.Width);

        return new([.. spans, .. Spans])
        {
            Insets = Insets.Count == 0 ? Insets : [.. Insets.Select(inset => inset.ShiftedBy(shift))],
        };
    }
}
