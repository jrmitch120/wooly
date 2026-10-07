using System.Drawing;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Media;

/// <summary>
///     Says which pictures a frame wants of the cache, and puts <b>drawn</b> pictures on screen, a frame at a time
///     (#363): through boxes on a sixel terminal and on a Kitty terminal that does not draw placeholders, and as
///     placements for the view to paint as placeholder cells on one that does — sending each picture, and forgetting
///     what the cache let go of and the Stand-in blurs no longer near the page (ADR-0016, ADR-0022, ADR-0023, #362).
///     Beneath the view that paints the rows, which hands it each frame's rows, where the scroll has got to, the page
///     and the <see cref="Raster" /> — and is left to paint text (#355, #361).
/// </summary>
/// <remarks>
///     The boxes ride the rows: they are placed from the same scroll position the text is drawn at, on every frame, so
///     a picture cannot come adrift from the post it belongs to (ADR-0016). They are a pool rather than a box per
///     attachment: a feed of twenty posts is drawn on every keypress, and building and disposing a view per picture per
///     frame would be the cost of scrolling. A box with nothing in it — a picture still on its way, or one that could
///     not be had — is released rather than drawn empty, which leaves the row saying <c>▒▒▒▒</c> and what it shows as
///     the whole of the answer.
///     <para>
///         Every box is either given a place or released, on every frame and with no path out that does neither — and
///         everything is released before anything is placed, so a picture is never put on screen over one the terminal
///         has not yet been told to drop. That invariant is the whole of what keeps a picture from sticking, and it
///         holds because <see cref="Boxes" /> answers for every box at once: a box is kept only where it has been given
///         the very picture it is already holding, so being kept and being placed are the same list rather than two
///         lists that can disagree.
///     </para>
/// </remarks>
internal sealed class Placing
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
    ///         Fixed, and every one of them built as this is, before anything is drawn, because the alternative is
    ///         adding a subview from inside a draw — which mutates the tree the draw is walking, and leaves the release
    ///         of a vanished picture depending on whether this frame happened to be the one that grew the pool. A stale
    ///         Kitty placement is not erased by drawing text over it (ADR-0016), so that shows up as a picture stuck
    ///         over somebody's post.
    ///     </para>
    /// </remarks>
    public const int MostBoxes = 24;

    /// <summary>How many rows of a scroll the cuts of a sixel are encoded ahead of it (ADR-0023).</summary>
    private const int Ahead = 4;

    private readonly IPictures _pictures;
    private readonly SixelPictures _sixels;
    private readonly Placeholders? _placeholders;
    private readonly List<IPictureBox> _boxes = [];

    /// <summary>
    ///     The Stand-in blurs a Kitty terminal has been handed to hold, by <see cref="Drawn.Id" />, as of the last
    ///     frame. What <see cref="LetGoOfBlurs" /> tells it to forget once they are no longer near the page.
    /// </summary>
    private HashSet<string> _blursHeld = [];

    /// <summary>
    ///     Where the page began on the last frame there was a page, or <see langword="null" /> before the first frame of
    ///     the rows it is on — nothing to have moved from yet.
    /// </summary>
    private int? _lastTop;

    /// <summary>
    ///     Which way the page last moved — down positive, up negative — or nought for a page that has not moved yet.
    ///     What decides which way <see cref="Want" /> reaches further.
    /// </summary>
    private int _travel;

    /// <param name="pictures">Where the pixels for a drawn picture come from.</param>
    /// <param name="box">
    ///     What makes a box a picture is drawn through: one added to the view in the program
    ///     (<see cref="PictureView" />), and a fake in a test. Asked <see cref="MostBoxes" /> times, here.
    /// </param>
    /// <param name="sixels">What encodes a sixel's cuts, laid on the page's backdrop.</param>
    /// <param name="placeholders">
    ///     What a Kitty terminal holds, for drawing pictures as placeholder cells rather than through a box
    ///     (ADR-0022), or <see langword="null" /> where there is no such terminal to hold them.
    /// </param>
    public Placing(IPictures pictures, Func<IPictureBox> box, SixelPictures sixels, Placeholders? placeholders = null)
    {
        _pictures = pictures;
        _sixels = sixels;
        _placeholders = placeholders;

        for (var at = 0; at < MostBoxes; at++)
        {
            _boxes.Add(box());
        }
    }

    /// <summary>
    ///     Says that the rows the next frame hands over are another lot, or the same lot come back to — either way,
    ///     nothing has been moved through yet, so <see cref="Want" /> reaches two screens either side again and the
    ///     cuts of a sixel are encoded a row either way rather than ahead.
    /// </summary>
    public void Resume()
    {
        _lastTop = null;
        _travel = 0;
    }

    /// <summary>
    ///     Puts every picture where <paramref name="lines" /> say it goes, the page beginning on row
    ///     <paramref name="top" /> of them, and lets go of every box that has nothing to draw. Called once a frame,
    ///     before anything is drawn.
    /// </summary>
    /// <param name="lines">The rows, all of them.</param>
    /// <param name="top">Which of them the page begins on.</param>
    /// <param name="width">How many columns the page has.</param>
    /// <param name="height">
    ///     How many rows the page has — none where nothing can be drawn, which releases every box, so that nothing is
    ///     left showing from the last size there was over whatever replaces it.
    /// </param>
    /// <param name="raster">How this terminal paints pixels, as the frame was settled under.</param>
    /// <returns>The pictures to paint as placeholder cells — none but where the terminal draws placeholders.</returns>
    public IReadOnlyList<Placement> Frame(IReadOnlyList<Line> lines, int top, int width, int height, Raster raster)
    {
        var page = new Page(lines, top, width, height);

        // What pictures are drawn as this frame, if they are drawn as placeholders at all: the one place the Raster's
        // way is asked about them. Anywhere else nothing is sent that way, so nothing sent is near to be held.
        var drawing = raster.Way is PictureWay.Placeholders ? _placeholders : null;
        var moving = 0;

        // Said before a terminal that draws nothing is let off the rest, so the cache is told every frame there is a
        // page — but not of a frame with no page to be near.
        if (page.HasRoom)
        {
            moving = Moved(top);
            Want(page, raster);
        }

        if (!page.HasRoom || raster.Cell is not { } cell)
        {
            ReleaseAll();
            LetGoOfBlurs([]);
            LetGoOfPictures(drawing);
            _placeholders?.Flush();

            return [];
        }

        LetGoOfBlurs(drawing is null ? [] : BlursNear(page));
        LetGoOfPictures(drawing);

        // Whatever the terminal was told to let go of since the last frame, before anything is sent.
        _placeholders?.Flush();

        switch (raster.Way)
        {
            case PictureWay.Placeholders:
                // Released rather than merely unused, so that nothing drawn through a box before the terminal said it
                // draws placeholders is left on screen under them.
                ReleaseAll();

                return drawing is null ? [] : Sent(drawing, page, cell);

            case PictureWay.Sixel:
                Place(page, (box, inset, at, picture) =>
                {
                    // Only the part of the box on the page: a box straddling the top or bottom is framed to the rows
                    // still on it, so the picture is cut here, once per cut, rather than by the driver on every
                    // frame (#292).
                    if (OnPage(inset, at, page) is not var (frame, crop))
                    {
                        box.Release();

                        return;
                    }

                    Shown(box, frame, () => box.Show(
                        inset.Drawn.Id,
                        _sixels.Of(inset, picture, cell, crop, raster.SixelColours)));
                });

                Prepare(page, moving, cell, raster.SixelColours);

                break;

            case PictureWay.Kitty:
                // Through Kitty the image view draws the whole box, which it sends once and moves (ADR-0016).
                Place(page, (box, inset, at, picture) => Shown(
                    box,
                    new Rectangle(inset.Column, at, inset.Columns, inset.Rows),
                    () => box.Show(inset.Drawn.Id, picture)));

                break;

            default:
                ReleaseAll();

                break;
        }

        return [];
    }

    /// <summary>
    ///     Which way the page has moved since the last frame there was a page — down positive, up negative, and nought
    ///     for one standing still or on the first frame of its rows — remembered as the way the page last moved where
    ///     it moved at all. The one answer to which way the page is going, for what is wanted and what is encoded ahead.
    /// </summary>
    private int Moved(int top)
    {
        var moved = _lastTop is { } was ? Math.Sign(top - was) : 0;

        _lastTop = top;

        if (moved != 0)
        {
            _travel = moved;
        }

        return moved;
    }

    /// <summary>
    ///     Says which pictures this frame wants: those near enough to the screen to be worth having, nearest first,
    ///     with those on it marked, and no others.
    /// </summary>
    /// <remarks>
    ///     From the scroll the view hands over — the view is the one place that knows where the scroll has got to,
    ///     which is why this is said here, beneath it, and not by the post (ADR-0016, #363). An account of nothing but
    ///     photographs works out rows for every post it holds; sending for a picture from there would fetch and decode
    ///     the lot to draw the handful that fit, which is how this came to run a machine out of memory.
    ///     <para>
    ///         Three screens ahead the way the page last moved and one behind, or two either side of a page that has
    ///         not moved yet, so that a picture is usually there by the time it is scrolled to and the reader never
    ///         sees its Stand-in — and one who turns round still finds the screen behind them covered (#352). The way
    ///         the page last moved rather than the way it moved this frame: a frame drawn with the page standing still
    ///         is most often a picture landing, and reaching less far on it would abandon the fetches queued for the
    ///         far screen every time one of the near ones arrived.
    ///     </para>
    ///     <para>
    ///         Said for the whole frame at once, because the cache needs the whole of it: what is on screen is what it
    ///         must never let go of, and what is nearest is what it should keep longest (ADR-0025). A picture is on
    ///         screen where the row that wants it is, or where any of its box is — a photograph scrolled half off the
    ///         top has its row off the page and its lower half still on it.
    ///     </para>
    /// </remarks>
    private void Want(Page page, Raster raster)
    {
        var (lines, top, width, height) = page;

        var (above, below) = _travel switch
        {
            > 0 => (1, 3),
            < 0 => (3, 1),
            _ => (2, 2),
        };

        var from = top - (height * above);
        var to = top + height + (height * below);

        // The rows each picture takes, from the row that wants it to the foot of its box. Only a picture some row
        // wants is said at all, which is what keeps a warned post's pictures from being sent for (ADR-0016).
        var spans = new Dictionary<string, (Drawn Drawn, int First, int Last)>();

        // Widens the rows a picture is said to take to reach first to last as well, or says them where none were yet.
        void Widen(Drawn drawn, int first, int last) =>
            spans[drawn.Id] = spans.TryGetValue(drawn.Id, out var span)
                ? (drawn, Math.Min(span.First, first), Math.Max(span.Last, last))
                : (drawn, first, last);

        for (var at = 0; at < lines.Count; at++)
        {
            if (lines[at].Wants is { } drawn && at >= from && at < to)
            {
                Widen(drawn, at, at);
            }
        }

        for (var at = 0; at < lines.Count; at++)
        {
            foreach (var inset in lines[at].Insets.Where(inset => spans.ContainsKey(inset.Drawn.Id)))
            {
                Widen(inset.Drawn, at, at + inset.Rows - 1);
            }
        }

        var bottom = top + height - 1;

        // How many rows lie between a picture and the page: none for one on it.
        int Distance((Drawn Drawn, int First, int Last) span) =>
            span.Last < top ? top - span.Last : span.First > bottom ? span.First - bottom : 0;

        // With the room the frame gives, which the view read on the UI thread — the only one a view's size may be read
        // on — so that the cache decodes to it without ever asking the window (#359).
        _pictures.Want(
            [
                .. spans.Values
                        .OrderBy(Distance)
                        .Select(span => new WantedPicture(span.Drawn, OnScreen: Distance(span) == 0)),
            ],
            raster,
            width);
    }

    /// <summary>
    ///     The pictures to draw, with the row of the page each starts on — which may be above the top of it or run
    ///     past its bottom, for a box being scrolled past — and with <paramref name="near" />, those within that many
    ///     rows of the page as well.
    /// </summary>
    private List<(Inset Inset, int Top, Picture Picture)> Wanted(Page page, int near = 0)
    {
        var wanted = new List<(Inset, int, Picture)>();

        for (var at = 0; at < page.Lines.Count; at++)
        {
            foreach (var inset in page.Lines[at].Insets)
            {
                var from = at - page.Top;

                // Off the top or off the bottom. A box straddling either edge is kept and clipped, which is what
                // keeps a picture visible while it is being scrolled past rather than blinking out at the edge.
                if (from + inset.Rows <= -near || from >= page.Height + near)
                {
                    continue;
                }

                // A Stand-in's blur carries its own pixels and is never looked up: it is not the cache's to hold (#349).
                if ((inset.Blur ?? _pictures.Of(inset.Drawn)) is { } picture)
                {
                    wanted.Add((inset, from, picture));
                }
            }
        }

        return wanted;
    }

    /// <summary>
    ///     The pictures on the page that a Kitty terminal holds, sending any that are ready and have not been sent. A
    ///     picture still being encoded is left out, and its box keeps the rows it reserved until a redraw brings it.
    /// </summary>
    /// <remarks>
    ///     A screen either side of the page is encoded ahead — inside the reach the frame's pictures are wanted over,
    ///     so the pixels are usually here to encode — so that a picture scrolled to is usually ready rather than
    ///     encoded the frame it comes into view (ADR-0022).
    /// </remarks>
    private List<Placement> Sent(Placeholders drawing, Page page, CellSize cell)
    {
        var placed = new List<Placement>();

        foreach (var (inset, at, picture) in Wanted(page, near: page.Height))
        {
            if (at + inset.Rows <= 0 || at >= page.Height)
            {
                drawing.Prepare(inset, picture, cell);
            }
            else if (drawing.Ready(inset, picture, cell) is { } id)
            {
                placed.Add(new Placement(inset, at, id));
            }
        }

        return placed;
    }

    /// <summary>
    ///     The Stand-in blurs <see cref="Sent" /> is about to prepare or place this frame, by <see cref="Drawn.Id" />:
    ///     the same reach, a screen either side of the page.
    /// </summary>
    private HashSet<string> BlursNear(Page page) =>
    [
        .. Wanted(page, near: page.Height)
           .Where(wanted => wanted.Inset.Blur is not null)
           .Select(wanted => wanted.Inset.Drawn.Id),
    ];

    /// <summary>
    ///     Tells a Kitty terminal drawing placeholders to forget every Stand-in blur it was handed that is not
    ///     <paramref name="near" /> the page — replaced by the picture it stood in for, or scrolled away (#349). None is
    ///     near where nothing is sent that way, or there is no page to be near, and then every blur held is let go of,
    ///     rather than kept for a frame that may never come.
    /// </summary>
    /// <remarks>
    ///     A picture is forgotten when the cache lets go of it (ADR-0025), but a blur is never in the cache, so nothing
    ///     else would ever say so: a blur nobody deletes is an image the terminal holds for the rest of the run, which
    ///     is the invariant ADR-0022 keeps for pictures. Said before the frame's <see cref="Placeholders.Flush" />, so a
    ///     blur replaced this frame is gone from the terminal in the frame its picture is sent. One scrolled back to is
    ///     simply encoded and sent again — it is a few kilobytes. Through a box, the box's own release does this.
    /// </remarks>
    private void LetGoOfBlurs(HashSet<string> near)
    {
        if (_placeholders is null)
        {
            return;
        }

        foreach (var gone in _blursHeld.Where(id => !near.Contains(id)))
        {
            _placeholders.Drop(gone);
        }

        _blursHeld = near;
    }

    /// <summary>
    ///     Drains what the cache has let go of since the last frame and tells <paramref name="drawing" /> — a Kitty
    ///     terminal drawing placeholders — to forget each of them, every size it holds (ADR-0022): here, on the UI
    ///     thread, before the frame's <see cref="Placeholders.Flush" />, rather than wherever the cache let go of it.
    ///     One let go of as another landed is drained on the redraw that landing asked for. Where pictures are not
    ///     drawn as placeholders nothing was sent that way, so the list is drained and discarded.
    /// </summary>
    private void LetGoOfPictures(Placeholders? drawing)
    {
        var letGo = _pictures.Drain();

        if (drawing is null)
        {
            return;
        }

        foreach (var gone in letGo)
        {
            drawing.Drop(gone);
        }
    }

    private void ReleaseAll() => _boxes.ForEach(box => box.Release());

    /// <summary>
    ///     Gives each picture on the page a box and lets go of every other, all of them released before any is placed,
    ///     and <paramref name="show" /> putting each picture in the box it was given.
    /// </summary>
    private void Place(Page page, Action<IPictureBox, Inset, int, Picture> show)
    {
        var wanted = Wanted(page);

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
            if (drawing[at] is { } which)
            {
                var (inset, from, picture) = wanted[at];

                show(_boxes[which], inset, from, picture);
            }
        }
    }

    /// <summary>
    ///     A box shown its picture by <paramref name="show" />, framed at <paramref name="frame" />, and drawn where it
    ///     can be — never as coloured cells, whatever the image view would have been willing to do (ADR-0016).
    /// </summary>
    private static void Shown(IPictureBox box, Rectangle frame, Action show)
    {
        show();

        if (box.Frame != frame)
        {
            box.Frame = frame;
        }

        box.Visible = box.CanDraw;
    }

    /// <summary>
    ///     Starts encoding, off the UI thread, the cuts of the pictures at the edges of the page that the next few rows
    ///     of a scroll will want — including a box just off the page, about to come onto it.
    /// </summary>
    /// <remarks>
    ///     A box straddling the edge is cut a row differently on every step, and encoding the cut on the frame that
    ///     wants it was most of what a step cost once nothing else was encoded twice (#292). An encode takes longer
    ///     than the gap between two notches of a trackpad, so a row ahead is not far enough: <see cref="Ahead" /> rows
    ///     the way the page moved this frame, and one the other way for a reader who turns round. A page standing
    ///     still, or on the first frame of its rows, is cut a row either way. A box wholly on the page is the same cut
    ///     whichever way it moves, and costs nothing here.
    /// </remarks>
    /// <param name="page">The page.</param>
    /// <param name="moving">Which way the page moved this frame, as <see cref="Moved" /> says.</param>
    /// <param name="cell">How many pixels a cell is.</param>
    /// <param name="colours">How many colours a sixel is encoded in.</param>
    private void Prepare(Page page, int moving, CellSize cell, int colours)
    {
        foreach (var (inset, at, picture) in Wanted(page, near: Ahead))
        {
            var now = OnPage(inset, at, page)?.Crop;

            for (var rows = -Ahead; rows <= Ahead; rows++)
            {
                // The page moving down is a box moving up it, so a box's top a row higher.
                var ahead = moving != 0 && Math.Sign(rows) == -moving;

                if (rows == 0 || (!ahead && Math.Abs(rows) > 1))
                {
                    continue;
                }

                if (OnPage(inset, at + rows, page) is { Crop: var next } && next != now)
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
    private static (Rectangle Frame, SixelCrop Crop)? OnPage(Inset inset, int top, Page page)
    {
        var first = Math.Max(0, -top);
        var rows = Math.Min(inset.Rows, page.Height - top) - first;
        var columns = Math.Min(inset.Columns, page.Width - inset.Column);

        return rows < 1 || columns < 1
            ? null
            : (new Rectangle(inset.Column, top + first, columns, rows), new SixelCrop(first, rows, columns));
    }

    /// <summary>
    ///     The rows a frame hands over, the row of them the page begins on, and how many columns and rows the page has.
    /// </summary>
    private readonly record struct Page(IReadOnlyList<Line> Lines, int Top, int Width, int Height)
    {
        /// <summary>Whether there is a page at all — none where the window has shrunk to nothing.</summary>
        public bool HasRoom => Width > 0 && Height > 0;
    }

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
    private static int?[] Boxes(IReadOnlyList<string?> held, IReadOnlyList<string> wanted)
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
}
