using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The one view in the TUI that paints anything. It is handed rows of spans and a theme, and it turns each span's
///     role into an attribute — which is what makes "no view constructs a colour" (ADR-0014) true by construction
///     rather than by discipline: no other view has a way to.
/// </summary>
/// <param name="theme">What answers the roles.</param>
/// <param name="rows">The rows to draw, given how much room there is.</param>
internal sealed class PaintedView(ITheme theme, Func<int, int, IReadOnlyList<Line>> rows) : View
{
    private int _top;

    /// <summary>
    ///     Whether this view scrolls to keep the selected row on screen. The content region does; the rail and the two
    ///     chrome rows are always as tall as what they hold.
    /// </summary>
    public bool FollowsSelection { get; init; }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var width = Viewport.Width;
        var height = Viewport.Height;

        if (width <= 0 || height <= 0)
        {
            return true;
        }

        var lines = rows(width, height);

        _top = FollowsSelection ? ScrolledTo(lines, height) : 0;

        for (var row = 0; row < height; row++)
        {
            // Cleared first, in the theme's own background, so that a row which is shorter than the one it replaced
            // does not leave the tail of the old one behind it.
            SetAttribute(theme.For(Role.Body));
            AddStr(0, row, new string(' ', width));

            var at = _top + row;

            if (at < 0 || at >= lines.Count)
            {
                continue;
            }

            var column = 0;

            foreach (var span in lines[at].Spans)
            {
                if (column >= width)
                {
                    break;
                }

                var text = span.Text.Length > width - column ? span.Text[..(width - column)] : span.Text;

                SetAttribute(theme.For(span.Role));
                AddStr(column, row, text);

                column += text.Length;
            }
        }

        return true;
    }

    /// <summary>
    ///     Which row to draw first so that the selected one is on screen. Follows the selection rather than keeping a
    ///     scroll position of its own: what the reader picked out is the thing that has to be visible, and everything
    ///     that moves it — <c>j</c>, <c>k</c>, a post arriving — moves it deliberately.
    /// </summary>
    private int ScrolledTo(IReadOnlyList<Line> lines, int height)
    {
        var first = -1;
        var last = -1;

        for (var at = 0; at < lines.Count; at++)
        {
            if (!lines[at].Has(Role.Selection))
            {
                continue;
            }

            if (first < 0)
            {
                first = at;
            }

            last = at;
        }

        if (first < 0)
        {
            return Math.Clamp(_top, 0, Math.Max(0, lines.Count - height));
        }

        // A post taller than the terminal is shown from its top rather than its bottom: the byline is what says whose
        // post you are looking at, and losing that is worse than losing the last of the text.
        var top = last - height + 1 > _top ? last - height + 1 : Math.Min(_top, first);

        return Math.Clamp(top, 0, Math.Max(0, lines.Count - 1));
    }
}
