using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The one view in the TUI that paints anything. It is handed rows of spans and a theme, and it turns each span's
///     role into an attribute — which is what makes "no view constructs a colour" (ADR-0014) true by construction
///     rather than by discipline: no other view has a way to.
/// </summary>
/// <remarks>
///     The one thing it does not paint itself is a picture, which is drawn over the rows a post reserved for it by a
///     <see cref="PictureView" /> per box. Those boxes ride the rows: they are placed from the same scroll position the
///     text is drawn at, on every frame, so a picture cannot come adrift from the post it belongs to (ADR-0016).
/// </remarks>
internal sealed class PaintedView : View
{
    /// <summary>
    ///     How many pictures can be on screen at once. A picture is at least a few rows tall and a post's text stands
    ///     between one and the next, so a terminal cannot show many; this is generous for the tallest terminal anybody
    ///     reads a feed on.
    /// </summary>
    /// <remarks>
    ///     Fixed, and every one of them built before anything is drawn, because the alternative is adding a subview
    ///     from inside a draw — which mutates the tree the draw is walking, and leaves the release of a vanished
    ///     picture depending on whether this frame happened to be the one that grew the pool. A stale Kitty placement
    ///     is not erased by drawing text over it (ADR-0016), so that shows up as a picture stuck over somebody's post.
    /// </remarks>
    private const int MostBoxes = 8;

    private readonly ITheme _theme;
    private readonly Func<int, int, IReadOnlyList<Line>> _rows;
    private readonly IPictures? _pictures;
    private readonly List<PictureView> _boxes = [];

    private int _top;

    /// <param name="theme">What answers the roles.</param>
    /// <param name="rows">The rows to draw, given how much room there is.</param>
    /// <param name="pictures">
    ///     Where the pixels for a drawn attachment come from, or <see langword="null" /> for a region that shows no
    ///     posts — the rail and the two chrome rows, which reserve no boxes and would never ask.
    /// </param>
    public PaintedView(ITheme theme, Func<int, int, IReadOnlyList<Line>> rows, IPictures? pictures = null)
    {
        _theme = theme;
        _rows = rows;
        _pictures = pictures;

        if (pictures is null)
        {
            return;
        }

        for (var at = 0; at < MostBoxes; at++)
        {
            var box = new PictureView { Visible = false };

            _boxes.Add(box);
            Add(box);
        }
    }

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
            // Nothing can be drawn, so nothing may be left drawn either: a box still showing from the last size this
            // view had would be a picture over whatever replaces it.
            Hide(from: 0);

            return true;
        }

        var lines = _rows(width, height);

        _top = FollowsSelection ? ScrolledTo(lines, height) : 0;

        for (var row = 0; row < height; row++)
        {
            // Cleared first, in the theme's own background, so that a row which is shorter than the one it replaced
            // does not leave the tail of the old one behind it.
            SetAttribute(_theme.For(Role.Body));
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

                SetAttribute(_theme.For(span.Role));
                AddStr(column, row, text);

                column += text.Length;
            }
        }

        Place(lines, height);

        return true;
    }

    /// <summary>
    ///     Puts a picture over each box the rows reserved, in the same pass that drew the rows and from the same scroll
    ///     position, so that what is drawn and what is scrolled cannot disagree.
    /// </summary>
    /// <remarks>
    ///     The boxes are a pool rather than a view per attachment: a feed of twenty posts is drawn on every keypress,
    ///     and building and disposing a view per picture per frame would be the cost of scrolling. A box with nothing
    ///     in it — a picture still on its way, or one that could not be had — is hidden rather than drawn empty, which
    ///     leaves the row saying <c>▒▒▒▒</c> and what it shows as the whole of the answer.
    ///     <para>
    ///         Every box is either given a place here or hidden here, on every frame and with no path out that does
    ///         neither. What is hidden is what Terminal.Gui releases, and what it releases is what stops a Kitty
    ///         placement outliving the post it belonged to.
    ///     </para>
    /// </remarks>
    private void Place(IReadOnlyList<Line> lines, int height)
    {
        if (_pictures is null)
        {
            return;
        }

        var placed = 0;

        for (var at = 0; at < lines.Count && placed < _boxes.Count; at++)
        {
            foreach (var inset in lines[at].Insets)
            {
                var top = at - _top;

                // Off the top or off the bottom. A box straddling either edge is placed anyway and clipped, which is
                // what keeps a picture visible while it is being scrolled past rather than blinking out at the edge.
                if (top + inset.Rows <= 0 || top >= height || placed >= _boxes.Count)
                {
                    continue;
                }

                var box = _boxes[placed++];
                var frame = new Rectangle(inset.Column, top, inset.Columns, inset.Rows);

                box.Show(inset.Media.Id, _pictures.Of(inset.Media));

                // Only when it has actually moved: setting it throws away the scaled copy and re-encodes the picture,
                // which on a feed redrawn per keypress would be a re-transmission per keypress.
                if (box.Frame != frame)
                {
                    box.Frame = frame;
                }

                // Never drawn as coloured cells, whatever ImageView would have been willing to do (ADR-0016).
                box.Visible = box.HasPicture && box.CanDraw;
            }
        }

        Hide(from: placed);
    }

    /// <summary>Hides every box from <paramref name="from" /> on, which is every one this frame found no place for.</summary>
    private void Hide(int from)
    {
        for (var spare = from; spare < _boxes.Count; spare++)
        {
            _boxes[spare].Visible = false;
        }
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
