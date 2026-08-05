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

    private IReadOnlyList<Line>? _settled;
    private int _top;
    private bool _following = true;

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
    ///     Whether this view scrolls at all. The content region does; the rail and the two chrome rows are always as
    ///     tall as what they hold.
    /// </summary>
    public bool Scrolls { get; init; }

    /// <summary>
    ///     The item <c>j</c> and <c>k</c> should take back — the topmost one on the page — or <see langword="null" />
    ///     while the selection is still on screen and they can simply move from it. Never a row of its own: what is
    ///     reclaimed is the whole post the page begins on.
    /// </summary>
    /// <remarks>
    ///     Asked of the view because the view is the only thing that knows how much room there is and where the arrows
    ///     have left the scroll. The rows are worked out again to answer it, which costs what one frame costs and is
    ///     paid once per keypress rather than once per redraw.
    /// </remarks>
    public int? Reclaimable
    {
        get
        {
            var width = Viewport.Width;
            var height = Viewport.Height;

            if (!Scrolls || width <= 0 || height <= 0)
            {
                return null;
            }

            var lines = _rows(width, height);

            return Scroll.Shows(lines, height, _top) ? null : Scroll.Topmost(lines, _top);
        }
    }

    /// <summary>
    ///     Moves the screen by <paramref name="rows" /> and leaves the selection where it is, which is what <c>↓</c>
    ///     and <c>↑</c> do — and the only way to read a post taller than the terminal to its end.
    /// </summary>
    public void Step(int rows)
    {
        var width = Viewport.Width;
        var height = Viewport.Height;

        if (!Scrolls || width <= 0 || height <= 0)
        {
            return;
        }

        _top = Scroll.By(_rows(width, height), _top, rows);
        _following = false;

        SetNeedsDraw();
    }

    /// <summary>
    ///     The same, a screenful at a time, which is what <c>PgUp</c> and <c>PgDn</c> do. A screenful is however many
    ///     rows there is room for, so what was at the bottom of the page is at the top of the next one.
    /// </summary>
    public void Turn(int pages) => Step(pages * Viewport.Height);

    /// <summary>
    ///     Puts the screen back to following the selection, which is what moving the selection means: from here it is
    ///     brought into view again and kept there until the arrows take the scroll back over.
    /// </summary>
    public void Follow() => _following = true;

    /// <summary>
    ///     Starts again at the top, following. What a screen being replaced does — pushed, popped back to, or a
    ///     destination arrived at — because a row offset is about the rows it was made on and means nothing on
    ///     somebody else's.
    /// </summary>
    public void Restart()
    {
        _top = 0;
        _following = true;
    }

    /// <summary>
    ///     Where the pictures are put, which has to be before the boxes holding them draw — and Terminal.Gui draws a
    ///     view's SubViews <em>before</em> its content, so doing it from <see cref="OnDrawingContent" /> put every
    ///     picture where the rows wanted it one frame ago.
    /// </summary>
    /// <remarks>
    ///     That is the whole of why this is here rather than there. The symptom was precise: moving between posts a row
    ///     at a time left the pictures behind while the text moved, because each box drew at the place the frame before
    ///     had given it — while a page-jump, which carries a picture off screen entirely, was clean, since letting go of
    ///     one takes it off the terminal at once and needs no redraw to do it.
    ///     <para>
    ///         Clearing the viewport is the first thing a view does when it draws, so it is the last moment before the
    ///         boxes are drawn. Nothing is cleared differently for it: the answer is always <see langword="false" />,
    ///         which is "carry on".
    ///     </para>
    /// </remarks>
    protected override bool OnClearingViewport()
    {
        Settle();

        return false;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var width = Viewport.Width;
        var height = Viewport.Height;

        if (width <= 0 || height <= 0)
        {
            return true;
        }

        // Taken from the settling a moment ago rather than worked out again: where the scroll has got to is worked out
        // from where it was, so asking twice in one frame can answer twice differently — and text drawn at one scroll
        // position with pictures placed at another is the bug this whole arrangement exists to avoid.
        var lines = _settled ?? Rows(width, height);

        _settled = null;

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

        return true;
    }

    /// <summary>
    ///     Works out the rows and where the scroll has got to, and puts every picture where those rows say it goes.
    ///     Called before anything is drawn, for the reason <see cref="OnClearingViewport" /> gives.
    /// </summary>
    private void Settle()
    {
        _settled = null;

        var width = Viewport.Width;
        var height = Viewport.Height;

        if (width <= 0 || height <= 0)
        {
            // Nothing can be drawn, so nothing may be left drawn either: a box still showing from the last size this
            // view had would be a picture over whatever replaces it.
            _boxes.ForEach(box => box.Release());

            return;
        }

        var lines = Rows(width, height);

        Want(lines, height);
        Place(lines, height);

        _settled = lines;
    }

    /// <summary>
    ///     Sends for the pictures of the attachments near enough to the screen to be worth having, and for no others.
    /// </summary>
    /// <remarks>
    ///     The one place that knows where the scroll has got to, which is why this is the view's job and not the post's
    ///     (ADR-0016). An account of nothing but photographs works out rows for every post it holds; sending for a
    ///     picture from there would fetch and decode the lot to draw the handful that fit, which is how this came to
    ///     run a machine out of memory.
    ///     <para>
    ///         A screen's worth either side of what is showing, so that a picture is usually there by the time it is
    ///         scrolled to rather than arriving after it.
    ///     </para>
    /// </remarks>
    private void Want(IReadOnlyList<Line> lines, int height)
    {
        if (_pictures is null)
        {
            return;
        }

        var from = _top - height;
        var to = _top + (height * 2);

        for (var at = Math.Max(0, from); at < Math.Min(lines.Count, to); at++)
        {
            if (lines[at].Wants is { } media)
            {
                _pictures.Want(media);
            }
        }
    }

    /// <summary>The rows to draw, and where the scroll has got to.</summary>
    /// <remarks>
    ///     Following, that is the scroll that brings the selection into view; walked away from with the arrows, it is
    ///     wherever they left it, clamped to the rows there now — since a post taken down under a reader who had
    ///     scrolled to the foot of the screen leaves an offset past the end of it.
    /// </remarks>
    private IReadOnlyList<Line> Rows(int width, int height)
    {
        var lines = _rows(width, height);

        _top = Scrolls
            ? _following ? Scroll.To(lines, height, _top) : Scroll.By(lines, _top, 0)
            : 0;

        return lines;
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
    ///         Every box is either given a place here or released here, on every frame and with no path out that does
    ///         neither — and everything is released before anything is placed, so a picture is never put on screen over
    ///         one the terminal has not yet been told to drop.
    ///     </para>
    /// </remarks>
    private void Place(IReadOnlyList<Line> lines, int height)
    {
        if (_pictures is null)
        {
            return;
        }

        var wanted = Wanted(lines, height);

        // Let go first, and of everything, before anything is put anywhere. A box is released the moment its picture
        // stops being wanted, which is what tells the terminal to drop what is on it — doing that after placing the
        // rest would leave a frame in which the old placement is still on screen under the new one.
        var freed = new List<PictureView>();

        foreach (var box in _boxes)
        {
            if (box.MediaId is { } held && wanted.Any(want => want.Inset.Media.Id == held))
            {
                continue;
            }

            if (box.MediaId is not null)
            {
                freed.Add(box);
            }

            box.Release();
        }

        var taken = new List<PictureView>();

        foreach (var (inset, top, picture) in wanted)
        {
            if (Free(inset.Media.Id, freed, taken) is not { } box)
            {
                continue;
            }

            taken.Add(box);

            var frame = new Rectangle(inset.Column, top, inset.Columns, inset.Rows);

            box.Show(inset.Media.Id, picture);

            // Only when it has actually moved: setting it throws away the scaled copy and re-encodes the picture,
            // which on a feed redrawn per keypress would be a re-transmission per keypress.
            if (box.Frame != frame)
            {
                box.Frame = frame;
            }

            // Never drawn as coloured cells, whatever ImageView would have been willing to do (ADR-0016).
            box.Visible = box.CanDraw;
        }
    }

    /// <summary>
    ///     The pictures to draw this frame, with the row each starts on — which may be above the top of the view or
    ///     run past its bottom, for a box being scrolled past.
    /// </summary>
    private List<(Inset Inset, int Top, Picture Picture)> Wanted(IReadOnlyList<Line> lines, int height)
    {
        var wanted = new List<(Inset, int, Picture)>();

        for (var at = 0; at < lines.Count; at++)
        {
            foreach (var inset in lines[at].Insets)
            {
                var top = at - _top;

                // Off the top or off the bottom. A box straddling either edge is kept and clipped, which is what
                // keeps a picture visible while it is being scrolled past rather than blinking out at the edge.
                if (top + inset.Rows <= 0 || top >= height)
                {
                    continue;
                }

                if (_pictures!.Of(inset.Media) is { } picture)
                {
                    wanted.Add((inset, top, picture));
                }
            }
        }

        return wanted;
    }

    /// <summary>
    ///     The box to draw <paramref name="mediaId" /> in: the one already holding it, or one holding nothing.
    /// </summary>
    /// <remarks>
    ///     A box freed this very frame is the last resort, so that a view never goes from one picture straight to
    ///     another within a frame — the terminal is told to drop the first, and only a frame later is the second put
    ///     there. With room for eight and a terminal able to show a handful, that fallback is not reached in practice;
    ///     it is there so that a screen full of pictures draws them rather than dropping one.
    ///     <para>
    ///         A box already <paramref name="taken" /> this frame is never handed out again, because the same
    ///         attachment can be on screen twice — a boost and the post it boosts, both in one feed — and two boxes
    ///         sharing one view would be one picture drawn and the other silently lost.
    ///     </para>
    /// </remarks>
    private PictureView? Free(string mediaId, List<PictureView> freed, List<PictureView> taken) =>
        _boxes.FirstOrDefault(box => box.MediaId == mediaId && !taken.Contains(box))
        ?? _boxes.FirstOrDefault(box => box.MediaId is null && !freed.Contains(box) && !taken.Contains(box))
        ?? _boxes.FirstOrDefault(box => box.MediaId is null && !taken.Contains(box));
}
