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
///     text around it (ADR-0022). Anywhere else a picture is drawn over those rows by a <see cref="PictureView" /> per
///     box. Those boxes ride the rows: they are placed from the same scroll position the text is drawn at, on every
///     frame, so a picture cannot come adrift from the post it belongs to (ADR-0016).
/// </remarks>
internal sealed class PaintedView : View
{
    /// <summary>
    ///     How many pictures can be on screen at once. Generous for the tallest terminal anybody reads a feed on.
    /// </summary>
    /// <remarks>
    ///     Eight until a byline gained an avatar (#77), which is the change that made this a count of posts rather
    ///     than a count of attachments: an attachment is a few rows tall and a post's text stands between one and the
    ///     next, so a screen could only ever hold a handful — but every post wants a box now, and a tall terminal
    ///     shows a dozen posts before it shows a single photograph. A screen wanting more pictures than there are
    ///     boxes draws what it can and drops the rest, which is a picture silently missing.
    ///     <para>
    ///         Fixed, and every one of them built before anything is drawn, because the alternative is adding a
    ///         subview from inside a draw — which mutates the tree the draw is walking, and leaves the release of a
    ///         vanished picture depending on whether this frame happened to be the one that grew the pool. A stale
    ///         Kitty placement is not erased by drawing text over it (ADR-0016), so that shows up as a picture stuck
    ///         over somebody's post.
    ///     </para>
    /// </remarks>
    private const int MostBoxes = 24;

    /// <summary>How many rows of a scroll the cuts of a sixel are encoded ahead of it (ADR-0023).</summary>
    private const int Ahead = 4;

    private readonly ITheme _theme;
    private readonly Func<int, int, IReadOnlyList<Line>> _rows;
    private readonly IPictures? _pictures;
    private readonly Func<int, int, IReadOnlyList<Line>>? _frame;
    private readonly List<PictureView> _boxes = [];
    private readonly Placeholders? _placeholders;
    private readonly SixelPictures _sixels = new();

    /// <summary>Where the page began the last time pictures were placed, which says which way it is moving.</summary>
    private int _placedAt;

    /// <summary>The pictures drawn as placeholders this frame, with the row each starts on and its image id.</summary>
    private List<(Inset Inset, int Top, int Id)> _placed = [];

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
        Placeholders? placeholders = null)
    {
        _theme = theme;
        _rows = rows;
        _pictures = pictures;
        _frame = frame;
        _placeholders = pictures is null ? null : placeholders;

        if (frame is not null)
        {
            Padding.Thickness = new Thickness(1);
        }

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
    /// <remarks>
    ///     Settable rather than fixed at construction because the content region stops scrolling while a post is being
    ///     written, which ADR-0015 settled ("You cannot scroll the timeline while composing") and nothing enforced.
    ///     Turning it off pins the offset back to the top on the next frame, so a region already scrolled away is put
    ///     right rather than merely frozen where it was left.
    /// </remarks>
    public bool Scrolls { get; set; }

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
    ///     The frame, painted on the ring of padding round the viewport. Terminal.Gui clips this pass to that ring, so
    ///     the panel's rows are painted whole and only their edges land — the inside is the viewport's, drawn above.
    /// </summary>
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
            Paint(edges[row], -1, row - 1, width);
        }

        return true;
    }

    /// <summary>
    ///     One row, <paramref name="width" /> columns of it from <paramref name="left" />, cleared first and then its
    ///     spans painted in what the theme answers for each.
    /// </summary>
    private void Paint(Line? line, int left, int row, int width)
    {
        var picked = line?.Picked == true;

        // Cleared first, in the theme's own background, so that a row which is shorter than the one it replaced
        // does not leave the tail of the old one behind it — and on the band, for a row of the thing picked out,
        // so that the band runs to the edge of the view rather than stopping where the words do (#269).
        SetAttribute(picked ? _theme.Banded(Role.Body) : _theme.For(Role.Body));
        AddStr(left, row, new string(' ', width));

        if (line is null)
        {
            return;
        }

        var column = 0;

        foreach (var span in line.Spans)
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
            _placed = [];

            return;
        }

        var lines = Rows(width, height);

        Want(lines, height);

        // Whatever the terminal was told to let go of since the last frame, before anything is sent.
        _placeholders?.Flush();

        if (_placeholders?.Drawing == true)
        {
            // Released rather than merely unused, so that nothing drawn through a box before the terminal said it
            // speaks Kitty is left on screen under the placeholders.
            _boxes.ForEach(box => box.Release());
            _placed = Sent(lines, height);
        }
        else
        {
            _placed = [];
            Place(lines, height);
        }

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
            if (lines[at].Wants is { } drawn)
            {
                _pictures.Want(drawn);
            }
        }
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
    ///         one the terminal has not yet been told to drop. That invariant is the whole of what keeps a picture from
    ///         sticking, and it holds because <see cref="Boxes" /> answers for every box at once: a box is kept only
    ///         where it has been given the very picture it is already holding, so being kept and being placed are the
    ///         same list rather than two lists that can disagree.
    ///     </para>
    /// </remarks>
    private void Place(IReadOnlyList<Line> lines, int height)
    {
        if (_pictures is null)
        {
            return;
        }

        var wanted = Wanted(lines, height);

        if (_pictures.Cell is not { } cell)
        {
            _boxes.ForEach(box => box.Release());

            return;
        }

        var colours = SixelColours();

        // Who draws what, settled for the whole frame before anything moves — see Boxes. Asking box by box is what
        // this used to do, and it could not see that one picture was wanted once and held twice.
        var drawing = Boxes(
            [.. _boxes.Select(box => box.PictureId)],
            [.. wanted.Select(want => want.Inset.Drawn.Id)]);

        // Which boxes are already holding the picture they have been given, and so have nothing to be told.
        var keeping = new HashSet<int>();

        for (var at = 0; at < wanted.Count; at++)
        {
            if (drawing[at] is { } which && _boxes[which].PictureId == wanted[at].Inset.Drawn.Id)
            {
                keeping.Add(which);
            }
        }

        // Let go first, and of everything else, before anything is put anywhere. A box is released the moment it stops
        // holding a picture that is wanted where it is holding it — doing that after placing the rest would leave a
        // frame in which the old placement is still on screen under the new one.
        for (var box = 0; box < _boxes.Count; box++)
        {
            if (!keeping.Contains(box))
            {
                _boxes[box].Release();
            }
        }

        for (var at = 0; at < wanted.Count; at++)
        {
            if (drawing[at] is not { } which)
            {
                continue;
            }

            var (inset, top, picture) = wanted[at];
            var box = _boxes[which];

            // Through Kitty the image view draws the whole box, which it sends once and moves (ADR-0016).
            if (colours == 0)
            {
                var whole = new Rectangle(inset.Column, top, inset.Columns, inset.Rows);

                box.Show(inset.Drawn.Id, picture);

                if (box.Frame != whole)
                {
                    box.Frame = whole;
                }

                box.Visible = box.CanDraw;

                continue;
            }

            // Only the part of the box on the page: a box straddling the top or bottom is framed to the rows still on
            // it, so the picture is cut here, once per cut, rather than by the driver on every frame (#292).
            if (OnPage(inset, top, height) is not var (frame, crop))
            {
                box.Release();

                continue;
            }

            box.Show(inset.Drawn.Id, _sixels.Of(inset, picture, cell, crop, colours));

            if (box.Frame != frame)
            {
                box.Frame = frame;
            }

            // Never drawn as coloured cells, whatever ImageView would have been willing to do (ADR-0016).
            box.Visible = box.CanDraw;
        }

        if (colours > 0)
        {
            Prepare(lines, height, cell, colours);
        }
    }

    /// <summary>
    ///     Starts encoding, off the UI thread, the cuts of the pictures at the edges of the page that the next few rows
    ///     of a scroll will want — including a box just off the page, about to come onto it.
    /// </summary>
    /// <remarks>
    ///     A box straddling the edge is cut a row differently on every step, and encoding the cut on the frame that
    ///     wants it was most of what a step cost once nothing else was encoded twice (#292). An encode takes longer
    ///     than the gap between two notches of a trackpad, so a row ahead is not far enough: <see cref="Ahead" /> rows
    ///     the way the page is moving, and one the other way for a reader who turns round. A box wholly on the page is
    ///     the same cut whichever way it moves, and costs nothing here.
    /// </remarks>
    private void Prepare(IReadOnlyList<Line> lines, int height, CellSize cell, int colours)
    {
        var moving = Math.Sign(_top - _placedAt);

        _placedAt = _top;

        foreach (var (inset, top, picture) in Wanted(lines, height, near: Ahead))
        {
            var now = OnPage(inset, top, height)?.Crop;

            for (var rows = -Ahead; rows <= Ahead; rows++)
            {
                // The page moving down is a box moving up it, so a box's top a row higher.
                var ahead = moving != 0 && Math.Sign(rows) == -moving;

                if (rows == 0 || (!ahead && Math.Abs(rows) > 1))
                {
                    continue;
                }

                if (OnPage(inset, top + rows, height) is { Crop: var next } && next != now)
                {
                    _sixels.Prepare(inset, picture, cell, next, colours);
                }
            }
        }
    }


    /// <summary>
    ///     The part of a box whose top is on row <paramref name="top" /> of the page that is on it — its frame, and
    ///     which of its rows and columns those are — or <see langword="null" /> where none of it is.
    /// </summary>
    private (Rectangle Frame, SixelCrop Crop)? OnPage(Inset inset, int top, int height)
    {
        var first = Math.Max(0, -top);
        var rows = Math.Min(inset.Rows, height - top) - first;
        var columns = Math.Min(inset.Columns, Viewport.Width - inset.Column);

        return rows < 1 || columns < 1
            ? null
            : (new Rectangle(inset.Column, top + first, columns, rows), new SixelCrop(first, rows, columns));
    }

    /// <summary>
    ///     The pictures on the page that a Kitty terminal holds, sending any that are ready and have not been sent. A
    ///     picture still being encoded is left out, and its box keeps the rows it reserved until a redraw brings it.
    /// </summary>
    /// <remarks>
    ///     A screen either side of the page is encoded ahead, the same reach <see cref="Want" /> fetches over, so that a
    ///     picture scrolled to is usually ready rather than encoded the frame it comes into view (ADR-0022).
    /// </remarks>
    private List<(Inset Inset, int Top, int Id)> Sent(IReadOnlyList<Line> lines, int height)
    {
        if (_pictures?.Cell is not { } cell)
        {
            return [];
        }

        var placed = new List<(Inset, int, int)>();

        foreach (var (inset, top, picture) in Wanted(lines, height, near: height))
        {
            if (top + inset.Rows <= 0 || top >= height)
            {
                _placeholders!.Prepare(inset, picture, cell);
            }
            else if (_placeholders!.Ready(inset, picture, cell) is { } id)
            {
                placed.Add((inset, top, id));
            }
        }

        return placed;
    }


    /// <summary>
    ///     How many colours a sixel is encoded in on this terminal, or none where a box draws through Kitty instead:
    ///     all 256 sixel allows, or fewer where the terminal says it has fewer. Terminal.Gui's image view stops at 64,
    ///     at which a photograph's gradients break into patches (ADR-0023).
    /// </summary>
    private int SixelColours() =>
        App?.Driver is { } driver
        && RasterProtocol.Chosen(driver.SixelSupport, driver.KittyGraphicsSupport) is PictureWay.Sixel
            ? Math.Min(256, driver.SixelSupport!.MaxPaletteColors)
            : 0;

    /// <summary>
    ///     Which box draws which of the pictures wanted this frame: the box already holding one where there is one,
    ///     and never the same box twice.
    /// </summary>
    /// <remarks>
    ///     One picture may be wanted more than once on a page — an account's avatar over a run of their posts, or a
    ///     boost and the post it boosts — so a box is matched to a <em>place</em> a picture is wanted rather than to
    ///     the picture. Deciding box by box instead is what put a stuck avatar over somebody's post: asked whether the
    ///     picture it held was still wanted <em>anywhere</em>, both of two boxes holding one avatar said yes, one of
    ///     them was given the single place left, and the other was neither moved nor released — and a Kitty placement
    ///     nobody deletes is not erased by drawing text over it (ADR-0016).
    ///     <para>
    ///         A box holding nothing is preferred over one being released this frame, so that a view never goes from
    ///         one picture straight to another within a frame: the terminal is told to drop the first, and only a
    ///         frame later is the second put there. Reusing a released box is the last resort, there so that a screen
    ///         with more pictures on it than there are boxes draws what it can rather than dropping one.
    ///     </para>
    /// </remarks>
    /// <param name="held">What each box is holding, by index, and <see langword="null" /> for one holding nothing.</param>
    /// <param name="wanted">The pictures to draw, in the order they appear, by id — the same id may appear twice.</param>
    /// <returns>
    ///     The box drawing each wanted picture, by index into <paramref name="held" />, or <see langword="null" />
    ///     where there was no box left for it.
    /// </returns>
    internal static int?[] Boxes(IReadOnlyList<string?> held, IReadOnlyList<string> wanted)
    {
        var drawing = new int?[wanted.Count];
        var spoken = new bool[held.Count];

        // Three passes, in the order a box is preferred: one already holding this very picture, then one holding
        // nothing, then one whose picture is being dropped this frame anyway.
        for (var at = 0; at < wanted.Count; at++)
        {
            Take(at, box => held[box] == wanted[at]);
        }

        for (var at = 0; at < wanted.Count; at++)
        {
            Take(at, box => held[box] is null);
        }

        for (var at = 0; at < wanted.Count; at++)
        {
            Take(at, _ => true);
        }

        return drawing;

        void Take(int at, Func<int, bool> suits)
        {
            if (drawing[at] is not null)
            {
                return;
            }

            for (var box = 0; box < held.Count; box++)
            {
                if (!spoken[box] && suits(box))
                {
                    drawing[at] = box;
                    spoken[box] = true;

                    return;
                }
            }
        }
    }

    /// <summary>
    ///     The pictures to draw this frame, with the row each starts on — which may be above the top of the view or
    ///     run past its bottom, for a box being scrolled past — and with <paramref name="near" />, those within that
    ///     many rows of the page as well.
    /// </summary>
    private List<(Inset Inset, int Top, Picture Picture)> Wanted(IReadOnlyList<Line> lines, int height, int near = 0)
    {
        var wanted = new List<(Inset, int, Picture)>();

        for (var at = 0; at < lines.Count; at++)
        {
            foreach (var inset in lines[at].Insets)
            {
                var top = at - _top;

                // Off the top or off the bottom. A box straddling either edge is kept and clipped, which is what
                // keeps a picture visible while it is being scrolled past rather than blinking out at the edge.
                if (top + inset.Rows <= -near || top >= height + near)
                {
                    continue;
                }

                if (_pictures!.Of(inset.Drawn) is { } picture)
                {
                    wanted.Add((inset, top, picture));
                }
            }
        }

        return wanted;
    }

}
