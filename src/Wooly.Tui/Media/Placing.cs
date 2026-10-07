using System.Drawing;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Media;

/// <summary>
///     Puts <b>drawn</b> pictures on screen through boxes, a frame at a time: on a sixel terminal, and on a Kitty
///     terminal that does not draw placeholders (ADR-0016, ADR-0022, ADR-0023). Beneath the view that paints the rows,
///     which hands it each frame's rows, where the scroll has got to, the page and the <see cref="Raster" /> — and is
///     left to paint text (#355, #361).
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
    private readonly List<IPictureBox> _boxes = [];

    /// <summary>Where the page began when sixel cuts were last encoded ahead, which says which way it is moving.</summary>
    private int _placedAt;

    /// <param name="pictures">Where the pixels for a drawn picture come from.</param>
    /// <param name="box">
    ///     What makes a box a picture is drawn through: one added to the view in the program
    ///     (<see cref="PictureView" />), and a fake in a test. Asked <see cref="MostBoxes" /> times, here.
    /// </param>
    /// <param name="sixels">What encodes a sixel's cuts, laid on the page's backdrop.</param>
    public Placing(IPictures pictures, Func<IPictureBox> box, SixelPictures sixels)
    {
        _pictures = pictures;
        _sixels = sixels;

        for (var at = 0; at < MostBoxes; at++)
        {
            _boxes.Add(box());
        }
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
    public void Frame(IReadOnlyList<Line> lines, int top, int width, int height, Raster raster)
    {
        if (width <= 0 || height <= 0 || raster.Cell is not { } cell)
        {
            ReleaseAll();

            return;
        }

        switch (raster.Way)
        {
            case PictureWay.Sixel:
                Place(lines, top, width, height, (box, inset, at, picture) =>
                {
                    // Only the part of the box on the page: a box straddling the top or bottom is framed to the rows
                    // still on it, so the picture is cut here, once per cut, rather than by the driver on every
                    // frame (#292).
                    if (OnPage(inset, at, width, height) is not var (frame, crop))
                    {
                        box.Release();

                        return;
                    }

                    Shown(box, frame, () => box.Show(
                        inset.Drawn.Id,
                        _sixels.Of(inset, picture, cell, crop, raster.SixelColours)));
                });

                Prepare(lines, top, width, height, cell, raster.SixelColours);

                break;

            case PictureWay.Kitty:
                // Through Kitty the image view draws the whole box, which it sends once and moves (ADR-0016).
                Place(lines, top, width, height, (box, inset, at, picture) => Shown(
                    box,
                    new Rectangle(inset.Column, at, inset.Columns, inset.Rows),
                    () => box.Show(inset.Drawn.Id, picture)));

                break;

            default:
                // Drawn as placeholders, or not at all: released rather than merely unused, so that nothing drawn
                // through a box before the terminal said it draws placeholders is left on screen under them.
                ReleaseAll();

                break;
        }
    }

    /// <summary>
    ///     The pictures to draw, with the row of the page each starts on — which may be above the top of it or run
    ///     past its bottom, for a box being scrolled past — and with <paramref name="near" />, those within that many
    ///     rows of the page as well.
    /// </summary>
    internal static List<(Inset Inset, int Top, Picture Picture)> Wanted(
        IPictures pictures,
        IReadOnlyList<Line> lines,
        int top,
        int height,
        int near = 0)
    {
        var wanted = new List<(Inset, int, Picture)>();

        for (var at = 0; at < lines.Count; at++)
        {
            foreach (var inset in lines[at].Insets)
            {
                var from = at - top;

                // Off the top or off the bottom. A box straddling either edge is kept and clipped, which is what
                // keeps a picture visible while it is being scrolled past rather than blinking out at the edge.
                if (from + inset.Rows <= -near || from >= height + near)
                {
                    continue;
                }

                // A Stand-in's blur carries its own pixels and is never looked up: it is not the cache's to hold (#349).
                if ((inset.Blur ?? pictures.Of(inset.Drawn)) is { } picture)
                {
                    wanted.Add((inset, from, picture));
                }
            }
        }

        return wanted;
    }

    private void ReleaseAll() => _boxes.ForEach(box => box.Release());

    /// <summary>
    ///     Gives each picture on the page a box and lets go of every other, all of them released before any is placed,
    ///     and <paramref name="show" /> putting each picture in the box it was given.
    /// </summary>
    private void Place(
        IReadOnlyList<Line> lines,
        int top,
        int width,
        int height,
        Action<IPictureBox, Inset, int, Picture> show)
    {
        var wanted = Wanted(_pictures, lines, top, height);

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
    ///     the way the page is moving, and one the other way for a reader who turns round. A box wholly on the page is
    ///     the same cut whichever way it moves, and costs nothing here.
    /// </remarks>
    private void Prepare(IReadOnlyList<Line> lines, int top, int width, int height, CellSize cell, int colours)
    {
        var moving = Math.Sign(top - _placedAt);

        _placedAt = top;

        foreach (var (inset, at, picture) in Wanted(_pictures, lines, top, height, near: Ahead))
        {
            var now = OnPage(inset, at, width, height)?.Crop;

            for (var rows = -Ahead; rows <= Ahead; rows++)
            {
                // The page moving down is a box moving up it, so a box's top a row higher.
                var ahead = moving != 0 && Math.Sign(rows) == -moving;

                if (rows == 0 || (!ahead && Math.Abs(rows) > 1))
                {
                    continue;
                }

                if (OnPage(inset, at + rows, width, height) is { Crop: var next } && next != now)
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
    private static (Rectangle Frame, SixelCrop Crop)? OnPage(Inset inset, int top, int width, int height)
    {
        var first = Math.Max(0, -top);
        var rows = Math.Min(inset.Rows, height - top) - first;
        var columns = Math.Min(inset.Columns, width - inset.Column);

        return rows < 1 || columns < 1
            ? null
            : (new Rectangle(inset.Column, top + first, columns, rows), new SixelCrop(first, rows, columns));
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
