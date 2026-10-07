using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

// Terminal.Gui has a Glyphs of its own — its box-drawing characters — and this file is the one that draws into it.
using Glyphs = Wooly.Tui.Rendering.Glyphs;

namespace Wooly.Tui.Views;

/// <summary>
///     The one view in the TUI that paints anything. It is handed rows of spans and a theme, and it turns each span's
///     role into an attribute — which is what makes "no view constructs a colour" (ADR-0014) true by construction
///     rather than by discipline: no other view has a way to.
/// </summary>
/// <remarks>
///     On a Kitty terminal it paints the pictures too, as placeholder cells over the rows a post reserved for each: the
///     picture is sent to the terminal once and is part of the rows from then on, so it moves in the same frame as the
///     text around it (ADR-0022). What is sent, and where each placement starts, is <see cref="Placing" />'s, which
///     hands the placements back each frame for this to paint (#362). Anywhere else a picture is drawn over those
///     rows through a box, a <see cref="PictureView" /> added to this view, which <see cref="Placing" /> puts where
///     the rows say from the same scroll position the text is drawn at, on every frame, so a picture cannot come
///     adrift from the post it belongs to (ADR-0016, #361). Which pictures a frame wants of the cache is
///     <see cref="Placing" />'s too, from the scroll this works out and hands it: this is the one place that knows the
///     scroll, and holds no picture code but painting the placeholder cells it is handed back (#363).
/// </remarks>
internal sealed class PaintedView : View
{
    private readonly ITheme _theme;
    private readonly Func<int, int, IReadOnlyList<Line>> _rows;
    private readonly Func<int, int, IReadOnlyList<Line>>? _frame;
    private readonly Placing? _placing;
    private readonly SynchronizedFrames? _frames;
    private readonly Func<Raster> _raster;

    /// <summary>The pictures drawn as placeholders this frame, with the row each starts on and its image id.</summary>
    private IReadOnlyList<Placement> _placed = [];

    private IReadOnlyList<Line>? _settled;
    private int _top;
    private bool _following = true;
    private bool _anchoring;

    /// <param name="theme">What answers the roles.</param>
    /// <param name="rows">The rows to draw, given how much room there is.</param>
    /// <param name="pictures">
    ///     Where the pixels for a drawn attachment come from, or <see langword="null" /> for a region that shows no
    ///     posts — the rail and the two chrome rows, which reserve no boxes and would never ask.
    /// </param>
    /// <param name="frame">
    ///     The panel this region is drawn inside, given the whole of the view's width and height — <see cref="Panel" />'s
    ///     rows, of which only the edges are painted — or <see langword="null" /> for a region with no frame.
    /// </param>
    /// <param name="placeholders">
    ///     What a Kitty terminal holds, for drawing pictures as placeholder cells rather than through a box, or
    ///     <see langword="null" /> to draw every picture through a box (ADR-0022).
    /// </param>
    /// <param name="frames">What wraps a frame this view draws in synchronized output, if anything does.</param>
    /// <param name="raster">
    ///     How this terminal paints pixels, asked once a frame as the frame is settled (<see cref="Raster" />), or
    ///     <see langword="null" /> for a terminal that draws none.
    /// </param>
    /// <remarks>
    ///     A frame is laid on a one-cell <c>Padding</c> round the view, so everything measured off
    ///     <see cref="View.Viewport" /> — the rows' width and height, the scroll, a page's worth — is the inside of it,
    ///     and a picture, which Terminal.Gui clips to the viewport of the view holding it, can never be drawn on the
    ///     edge however far it has been scrolled past one (ADR-0021).
    /// </remarks>
    public PaintedView(
        ITheme theme,
        Func<int, int, IReadOnlyList<Line>> rows,
        IPictures? pictures = null,
        Func<int, int, IReadOnlyList<Line>>? frame = null,
        Placeholders? placeholders = null,
        SynchronizedFrames? frames = null,
        Func<Raster>? raster = null)
    {
        _theme = theme;
        _raster = raster ?? (() => Raster.None);
        _frames = frames;
        _rows = rows;
        _frame = frame;

        if (frame is not null)
        {
            Padding.Thickness = new Thickness(1);
        }

        // Every box added to this view now, before anything is drawn (Placing.MostBoxes says why).
        _placing = pictures is null
            ? null
            : new Placing(
                pictures,
                () => PictureView.AddedTo(this),
                new SixelPictures(backdrop: Backdrop),
                placeholders);
    }

    /// <summary>
    ///     Whether this view scrolls at all. The content region does; the rail and the two chrome rows are always as
    ///     tall as what they hold.
    /// </summary>
    /// <remarks>
    ///     Settable rather than fixed at construction because the content region stops scrolling while a post is being
    ///     written, which ADR-0015 settled ("You cannot scroll the timeline while composing") and nothing enforced.
    ///     Turning it off pins the offset back to the top on the next frame, so a region already scrolled away is put
    ///     right rather than merely frozen where it was left.
    /// </remarks>
    public bool Scrolls { get; set; }

    /// <summary>
    ///     How this terminal paints pixels as of the last frame settled, or <see cref="Media.Raster.None" /> before the
    ///     first. Worked out once a frame, before the rows are, and read rather than asked again everywhere else — the
    ///     rows laid out for a click or a key between frames included — so the rows, the boxes and the picture cache
    ///     all go by the one answer the page was drawn under (#357). Read on the UI thread.
    /// </summary>
    public Raster Raster { get; private set; } = Raster.None;

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

            return Reclaiming(_rows(width, height), height);
        }
    }

    /// <summary>
    ///     Which thing <c>[</c> or <c>]</c> lands on — the first of the headed run <paramref name="by" /> along — or
    ///     <see langword="null" /> where there is no run that way and the press does nothing at all (#166).
    /// </summary>
    /// <remarks>
    ///     Asked of the view for the reason <see cref="Reclaimable" /> is, and answered from one lot of rows rather
    ///     than two: whether the pick is still on the page settles where the jump runs from, and that is a question
    ///     only something knowing the room and the offset can put.
    /// </remarks>
    public int? Along(int by)
    {
        var width = Viewport.Width;
        var height = Viewport.Height;

        if (!Scrolls || width <= 0 || height <= 0)
        {
            return null;
        }

        var lines = _rows(width, height);

        return Sections.Along(lines, by, Reclaiming(lines, height));
    }

    /// <summary>
    ///     Which thing the row under the terminal cell <paramref name="screen" /> is part of (<see cref="Line.Item" />),
    ///     or <see langword="null" /> for a cell outside the rows — on the frame, or below the last of them — or on a row
    ///     that is part of nothing. What a click is answered from (#286).
    /// </summary>
    public int? ItemAt(Point screen) => LineAt(screen, out _)?.Item;

    /// <summary>
    ///     Which thing the run under the terminal cell <paramref name="screen" /> stands for (<see cref="Span.Item" />),
    ///     or <see langword="null" /> for a cell outside the rows, past the end of its row, or on a run that stands for
    ///     nothing. What a click on one of a row's several things is answered from — a crumb of the breadcrumb (#308).
    /// </summary>
    public int? SpanItemAt(Point screen)
    {
        if (LineAt(screen, out var column) is not { } line)
        {
            return null;
        }

        foreach (var span in line.Spans)
        {
            if (column < span.Width)
            {
                return span.Item;
            }

            column -= span.Width;
        }

        return null;
    }

    /// <summary>
    ///     The row drawn under the terminal cell <paramref name="screen" />, and how far into it the cell is, or
    ///     <see langword="null" /> for a cell outside the rows: on the frame, or below the last of them.
    /// </summary>
    /// <remarks>
    ///     Asked of the view for the reason <see cref="Reclaimable" /> is: the rows a reader is pointing at are the ones
    ///     drawn at this width from where the page last began, and only the view knows both.
    /// </remarks>
    private Line? LineAt(Point screen, out int column)
    {
        var width = Viewport.Width;
        var height = Viewport.Height;
        var inside = ViewportToScreen(new Rectangle(Point.Empty, Viewport.Size));

        column = screen.X - inside.X;

        if (width <= 0 || height <= 0 || !inside.Contains(screen))
        {
            return null;
        }

        var lines = _rows(width, height);
        var at = _top + screen.Y - inside.Y;

        return at < lines.Count ? lines[at] : null;
    }

    /// <summary>
    ///     What a key that moves the pick has to take back before it moves it: the topmost thing on the page, where
    ///     the pick has none of its rows on it, and <see langword="null" /> while it is still visible.
    /// </summary>
    /// <remarks>
    ///     Said once for the two keys that ask it — <c>j</c>/<c>k</c> through <see cref="Reclaimable" /> and
    ///     <c>[</c>/<c>]</c> through <see cref="Along" /> — and taking the rows it is to answer over, because the one
    ///     thing those two must not do is work the rows out twice and reclaim off two different lots.
    /// </remarks>
    private int? Reclaiming(IReadOnlyList<Line> lines, int height) =>
        Scroll.Shows(lines, height, _top) ? null : Scroll.Topmost(lines, _top);

    /// <summary>
    ///     Moves the screen by <paramref name="rows" /> and leaves the selection where it is, which is what <c>↓</c>
    ///     and <c>↑</c> do — and the only way to read a post taller than the terminal to its end.
    /// </summary>
    /// <remarks>
    ///     Not held to the rows there are until it draws, which is the one place that knows them. Working them out
    ///     here to clamp against would lay out every post on the screen twice for one keypress — and a keypress is
    ///     what this is answering, so that cost is the whole of what a reader feels as a slow scroll. The far end is
    ///     the draw's to enforce, and it already does; only the near end is free to settle here.
    /// </remarks>
    public void Step(int rows)
    {
        if (!Scrolls || Viewport.Width <= 0 || Viewport.Height <= 0)
        {
            return;
        }

        _top = (int)Math.Clamp((long)_top + rows, 0, int.MaxValue);
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
    ///     Leaves the page where it begins now, whatever is picked next, as the arrows do — which is what a click asks
    ///     for, since the reader picked the thing where they could see it (#290).
    /// </summary>
    public void Hold() => _following = false;

    /// <summary>
    ///     Puts the screen back to following the selection and, on the very next frame only, brings the heading of the
    ///     run it is in onto the page with it — what <c>[</c> and <c>]</c> ask for over and above what <c>j</c> and
    ///     <c>k</c> do (#166).
    /// </summary>
    /// <remarks>
    ///     The next frame only, because the anchor is about the press rather than about the position: a reader who
    ///     walks down out of a long run with <c>k</c> has left its heading behind on purpose, and a page that went on
    ///     asking for it would be yanked back up the moment the heading scrolled off.
    /// </remarks>
    public void Anchor()
    {
        _following = true;
        _anchoring = true;

        SetNeedsDraw();
    }

    /// <summary>
    ///     Which row the page begins on: the one fact this view owns outright, and the one every other answer here is
    ///     derived from.
    /// </summary>
    /// <remarks>
    ///     Readable so that a test can see where the page actually is, rather than only what the view says about it.
    ///     <see cref="Into" /> is measured from whatever post is topmost and so agrees with itself wherever the page
    ///     has landed — it cannot tell "left alone" from "jumped by exactly one post", which is the shape every wrong
    ///     answer to #84 has taken.
    /// </remarks>
    internal int Top => _top;

    /// <summary>
    ///     Whether the page is still following the selection, or has been walked away from it with <c>↓</c> and
    ///     <c>↑</c>. The other half of where a reader was left, which is why a screen being drilled off keeps it
    ///     beside the row (#133).
    /// </summary>
    internal bool Following => _following;

    /// <summary>
    ///     Puts the page where the screen now being shown was left: at <paramref name="top" />, following the
    ///     selection again only if it was still following when the reader drilled off it. The one thing a screen being
    ///     replaced does to the offset, whichever kind of replacement it is (#133).
    /// </summary>
    /// <remarks>
    ///     Which is what makes the four replacements answer differently without anything here asking which one it is.
    ///     A screen nobody has read yet remembers row 0 and following, so a push, a destination arrived at and a
    ///     refresh all start again at the top — a row offset is about the rows it was made on and means nothing on
    ///     another lot, and what <c>g</c> is for is seeing what has arrived, which is above everything already read
    ///     (#84). Only a pop hands back a screen that remembers anything, and its rows are the very rows the offset
    ///     was made on.
    ///     <para>
    ///         Starting again at the top is where the pick is on every screen but one, since they open on their first
    ///         thing; the post screen opens on a post with its ancestors drawn above it, and following is what carries
    ///         the page down to it on the first draw (#86).
    ///     </para>
    ///     <para>
    ///         Carrying the follow flag matters as much as the row: a reader who had walked the page away from the
    ///         pick with <c>↓</c> should come back to what they were reading rather than be snapped onto the pick by
    ///         the first frame.
    ///     </para>
    ///     <para>
    ///         Not held to the rows there are, for the reason <see cref="Step" /> is not: the offset can be stale by
    ///         the time it is walked back to — the terminal resized, a post deleted out of the screen, a picture
    ///         changing how tall a post is — and the draw's clamp is what settles that, on rows it has in hand.
    ///     </para>
    /// </remarks>
    /// <param name="top">The row the page began on.</param>
    /// <param name="following">Whether it was still following the selection.</param>
    public void Resume(int top, bool following)
    {
        _top = top;
        _following = following;

        // Rows that are another lot, or the same lot come back to: either way nothing has been moved through yet.
        _placing?.Resume();
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
    ///         boxes are drawn. And the viewport is not cleared at all — the answer is <see langword="true" />, "done" —
    ///         because <see cref="OnDrawingContent" /> paints every cell of every row of it anyway, and clearing first
    ///         was painting the page twice a frame (#292).
    ///     </para>
    /// </remarks>
    protected override bool OnClearingViewport()
    {
        // Before anything of this frame can have been written: it is still being drawn.
        _frames?.Open();
        Settle();

        return true;
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
            var at = _top + row;

            Paint(at >= 0 && at < lines.Count ? lines[at] : null, 0, row, width);
        }

        PaintPlaceholders(lines, width, height);

        return true;
    }

    /// <summary>
    ///     Every row of every picture placed this frame that is on the page, as placeholder cells over the rows its post
    ///     reserved — including the lower rows of a box whose top has been scrolled off, which the terminal crops
    ///     (ADR-0022).
    /// </summary>
    private void PaintPlaceholders(IReadOnlyList<Line> lines, int width, int height)
    {
        foreach (var (inset, top, id) in _placed)
        {
            // Cut at the right edge, where the terminal draws the left of the picture and no more.
            var columns = Math.Min(inset.Columns, width - inset.Column);

            if (columns < 1)
            {
                continue;
            }

            for (var row = Math.Max(0, -top); row < inset.Rows && top + row < height; row++)
            {
                var at = _top + top + row;
                var picked = at < lines.Count && lines[at].Picked;

                // On the band where the post is picked, so a picture with transparency in it shows what the row is on.
                SetAttribute(KittyPlaceholder.Painted(id, picked ? _theme.Banded(Role.Body) : _theme.For(Role.Body)));
                AddStr(inset.Column, top + row, KittyPlaceholder.Row(row, columns));
            }
        }
    }

    /// <summary>
    ///     The frame, painted on the ring of padding round the viewport: its top and bottom edges whole, and of every
    ///     row between them only the two cells at its ends — the inside is the viewport's, drawn above.
    /// </summary>
    /// <remarks>
    ///     Terminal.Gui clips this pass to the ring, so painting the rows between whole landed only their ends anyway,
    ///     and paid for every cell inside them first: about half of what the content panel cost to paint (#292).
    /// </remarks>
    protected override bool OnDrawingAdornments()
    {
        if (_frame is null)
        {
            return false;
        }

        var width = Frame.Width;
        var edges = _frame(width, Frame.Height);

        // Counted from the viewport's corner, which the frame sits one cell above and to the left of.
        for (var row = 0; row < edges.Count; row++)
        {
            if (row == 0 || row == edges.Count - 1 || !PaintEnds(edges[row], row - 1, width))
            {
                Paint(edges[row], -1, row - 1, width);
            }
        }

        return true;
    }

    /// <summary>
    ///     The cell at each end of a frame's row and nothing between, where the row is one cell at each end around
    ///     whatever is inside — which a panel's row is (<see cref="Panel.Between" />). Answers whether it was.
    /// </summary>
    private bool PaintEnds(Line line, int row, int width)
    {
        if (line.Spans is not [var left, .., var right] || left.Width != 1 || right.Width != 1 || line.Width != width)
        {
            return false;
        }

        var picked = line.Picked;

        SetAttribute(picked ? _theme.Banded(left.Role) : _theme.For(left.Role));
        AddStr(-1, row, left.Text);
        SetAttribute(picked ? _theme.Banded(right.Role) : _theme.For(right.Role));
        AddStr(width - 2, row, right.Text);

        return true;
    }

    /// <summary>
    ///     One row, <paramref name="width" /> columns of it from <paramref name="left" />: its spans painted in what
    ///     the theme answers for each, and the rest of the row cleared after them.
    /// </summary>
    private void Paint(Line? line, int left, int row, int width)
    {
        var picked = line?.Picked == true;
        var column = 0;

        foreach (var span in line?.Spans ?? [])
        {
            if (column >= width)
            {
                break;
            }

            // Cut and stepped along in the columns a terminal draws in rather than in characters: the run being
            // painted is the one thing here that knows both, and a row of two-column characters cut by its
            // characters is a row painted twice as far right as it was laid out (#207).
            var text = Glyphs.Cut(span.Text, width - column);

            // The theme's to answer, not the view's: only a span sitting on the page goes onto the band, and one
            // with a background of its own — a picked reference a theme has given one — keeps it.
            SetAttribute(picked ? _theme.Banded(span.Role) : _theme.For(span.Role));
            AddStr(left + column, row, text);

            column += Glyphs.Columns(text);
        }

        // The rest cleared, in the theme's own background, so that a row which is shorter than the one it replaced
        // does not leave the tail of the old one behind it — and on the band, for a row of the thing picked out, so
        // that the band runs to the edge of the view rather than stopping where the words do (#269). After the spans
        // rather than under them, so that each cell is painted once (#292).
        if (column < width)
        {
            SetAttribute(picked ? _theme.Banded(Role.Body) : _theme.For(Role.Body));
            AddStr(left + column, row, new string(' ', width - column));
        }
    }

    /// <summary>
    ///     Works out the rows and where the scroll has got to, and puts every picture where those rows say it goes.
    ///     Called before anything is drawn, for the reason <see cref="OnClearingViewport" /> gives.
    /// </summary>
    private void Settle()
    {
        _settled = null;
        Raster = _raster();

        var width = Viewport.Width;
        var height = Viewport.Height;

        if (width <= 0 || height <= 0)
        {
            // Nothing can be drawn, so nothing may be left drawn either: a box still showing from the last size this
            // view had would be a picture over whatever replaces it, and a blur held would be held for nothing.
            _placed = _placing?.Frame([], _top, width, height, Raster) ?? [];

            return;
        }

        var lines = Rows(width, height);

        // Every picture wanted of the cache, every box placed or let go of, and every picture sent or forgotten, from
        // the scroll worked out here and by the Raster's way (#361, #362, #363).
        _placed = _placing?.Frame(lines, _top, width, height, Raster) ?? [];

        _settled = lines;
    }

    /// <summary>The rows to draw, and where the scroll has got to.</summary>
    /// <remarks>
    ///     Following, that is the scroll that brings the selection into view; walked away from with the arrows, it is
    ///     wherever they left it, clamped to the rows there now — since a post taken down under a reader who had
    ///     scrolled to the foot of the screen leaves an offset past the end of it.
    ///     <para>
    ///         An anchor asked for by <c>[</c> or <c>]</c> is spent here, on the one frame that follows the press:
    ///         this is called once a frame, and the anchor is about that press rather than about where the page has
    ///         come to rest (<see cref="Anchor" />).
    ///     </para>
    /// </remarks>
    private IReadOnlyList<Line> Rows(int width, int height)
    {
        var lines = _rows(width, height);
        var anchoring = _anchoring;

        _anchoring = false;

        _top = Scrolls
            ? _following
                ? anchoring ? Scroll.ToSection(lines, height, _top) : Scroll.To(lines, height, _top)
                : Scroll.By(lines, _top, 0)
            : 0;

        return lines;
    }

    /// <summary>
    ///     The page a sixel's transparent pixels are laid on: the theme's, where it names one, and otherwise the
    ///     terminal's own background as it answered when asked at startup — every built-in theme draws on that. Nothing
    ///     where neither is known, which leaves them transparent.
    /// </summary>
    private Terminal.Gui.Drawing.Color? Backdrop()
    {
        var none = Terminal.Gui.Drawing.Color.None;
        var page = _theme.For(Role.Body).Background;

        if (_theme.DrawsColour && page != none && page != Terminal.Gui.Drawing.Attribute.Default.Background)
        {
            return page;
        }

        return App?.Driver?.DefaultAttribute?.Background is { } terminal && terminal != none ? terminal : null;
    }
}
