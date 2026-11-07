using Wooly.Tui.Media;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Screens;

/// <summary>
///     Each thing's rows on a list that grows as it is read — its posts, its notifications, its conversations — stamped,
///     kept from one frame to the next and laid out again only where something they are drawn from has changed (#584).
///     A frame laid out every thing on its screen afresh, so a scroll slowed as the list grew: 12 ms a frame over 40
///     posts, 52 over 800, and most of it the garbage of rows nobody could see.
/// </summary>
/// <remarks>
///     A thing's rows are drawn from:
///     <list type="bullet">
///         <item>the thing — the same one, since a mark, an edit or a vote hands the list a new one;</item>
///         <item>its ordinal on the screen, whether it is picked, and the room it is drawn in;</item>
///         <item>
///             what the drawing says but for the clock, the page and the pictures: the Raster, the height, the caption
///             hidden or not, the blurs. No list's rows keep to the page (<see cref="Drawing.Top" />).
///         </item>
///         <item>
///             what else its screen draws it from, said by the screen as a value: what its reader has done to it
///             (<see cref="ReadAs" />), whether it is unread, and how long ago its rows say — the clock only as far as
///             that (<see cref="PostLines.FeedAge" />);
///         </item>
///         <item>
///             the pictures, only as far as what laying it out asked of them: every question asked, and its answer, is
///             kept with the rows, and they are kept only while every one is answered the same. A picture landing,
///             refused or let go of lays out again just the things that asked about it — and rows never hold on to a
///             picture the cache has let go of past the next frame.
///         </item>
///     </list>
/// </remarks>
/// <typeparam name="T">What the list holds, told apart by which one it is rather than by what it says.</typeparam>
/// <typeparam name="TAlso">What else its screen draws a thing's rows from, compared by what it says.</typeparam>
/// <remarks>
///     The drawing's height is part of what rows are kept by though no list's rows are drawn from it, so that a row
///     drawn from it one day is never kept wrongly: a terminal made taller or shorter lays a list out once more, which
///     is rare. Asked on the UI thread only, as every screen's rows are.
/// </remarks>
internal sealed class KeptRows<T, TAlso>
    where T : class
    where TAlso : IEquatable<TAlso>
{
    private readonly Dictionary<T, Kept> _kept = new(ReferenceEqualityComparer.Instance);

    /// <summary>The drawing last asked under, and it as rows are kept by: made once a frame rather than once a thing.</summary>
    private (Drawing Asked, Drawing KeptBy)? _drawing;

    /// <summary>
    ///     <paramref name="thing" />'s rows, stamped as the <paramref name="at" />th thing on the screen, picked out or
    ///     not: those kept from before where nothing they are drawn from has changed, and laid out afresh by
    ///     <paramref name="layOut" /> where it has.
    /// </summary>
    /// <param name="alsoDrawnFrom">Whatever else the screen draws the thing's rows from.</param>
    /// <param name="layOut">
    ///     How the thing's own rows are laid out, under the drawing given and in the room its gutter leaves it.
    /// </param>
    public IReadOnlyList<Line> Rows(
        T thing,
        int at,
        bool picked,
        Drawing drawing,
        TAlso alsoDrawnFrom,
        Func<Drawing, int, IReadOnlyList<Line>> layOut)
    {
        if (_drawing is not { } last || !ReferenceEquals(last.Asked, drawing))
        {
            _drawing = last = (drawing, drawing with { Now = default, Top = 0, Pictures = null });
        }

        var drawnFrom = new DrawnFrom(at, picked, last.KeptBy, alsoDrawnFrom);

        if (_kept.TryGetValue(thing, out var kept) && kept.DrawnFrom == drawnFrom && kept.Asked.SameNow(drawing.Pictures))
        {
            return kept.Rows;
        }

        var asked = new Asked(drawing.Pictures);
        var rows = Stamped.Rows(layOut(drawing with { Pictures = asked.Recording }, Stamped.Room(drawing.Width)), at, picked);

        _kept[thing] = new Kept(drawnFrom, asked, rows);

        return rows;
    }

    /// <summary>Lets go of the rows of everything but <paramref name="things" />, once there are more of them kept.</summary>
    public void KeepOnly(IReadOnlyList<T> things)
    {
        if (_kept.Count <= things.Count)
        {
            return;
        }

        var still = new HashSet<T>(things, ReferenceEqualityComparer.Instance);

        foreach (var gone in _kept.Keys.Where(thing => !still.Contains(thing)).ToList())
        {
            _kept.Remove(gone);
        }
    }

    private sealed record Kept(DrawnFrom DrawnFrom, Asked Asked, IReadOnlyList<Line> Rows);

    /// <summary>Everything a thing's rows are drawn from but the thing and the pictures.</summary>
    private sealed record DrawnFrom(int At, bool Picked, Drawing Drawing, TAlso AlsoDrawnFrom);

    /// <summary>
    ///     What laying out one thing asked of the pictures, and what it was answered: asked through
    ///     <see cref="Recording" />, which answers as the pictures it stands in front of do.
    /// </summary>
    private sealed class Asked(IPictures? pictures)
    {
        /// <summary>The pictures, as laying the thing out asks them; none where there are none.</summary>
        public IPictures? Recording { get; } = pictures is null ? null : new Recorder(pictures);

        /// <summary>Whether <paramref name="now" /> answers every question asked just as it was answered then.</summary>
        public bool SameNow(IPictures? now)
        {
            if (Recording is not Recorder recorder)
            {
                return now is null;
            }

            if (now is null)
            {
                return false;
            }

            foreach (var (drawn, answer) in recorder.Of)
            {
                if (!answer.Is(now.Of(drawn)))
                {
                    return false;
                }
            }

            foreach (var (drawn, answer) in recorder.WhyNot)
            {
                if (now.WhyNot(drawn) != answer)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class Recorder(IPictures pictures) : IPictures
        {
            public List<(Drawn Drawn, Answered Answer)> Of { get; } = [];

            public List<(Drawn Drawn, Unshowable? Answer)> WhyNot { get; } = [];

            Picture? IPictures.Of(Drawn drawn)
            {
                var answer = pictures.Of(drawn);

                Of.Add((drawn, new Answered(answer)));

                return answer;
            }

            Unshowable? IPictures.WhyNot(Drawn drawn)
            {
                var answer = pictures.WhyNot(drawn);

                WhyNot.Add((drawn, answer));

                return answer;
            }

            public void Want(IReadOnlyList<WantedPicture> frame, Raster raster, int columns) =>
                pictures.Want(frame, raster, columns);

            public IReadOnlyList<string> Drain() => pictures.Drain();
        }

        /// <summary>
        ///     A picture laying a thing out was answered with, held weakly: the rows of a screen under the one in front
        ///     are kept and not drawn, and holding the picture would keep its pixels past the cache's budget until that
        ///     screen was walked back out of (ADR-0025). One the cache let go of and nothing else holds is gone, which
        ///     answers differently from anything the cache can answer now.
        /// </summary>
        private sealed class Answered(Picture? picture)
        {
            private readonly WeakReference<Picture>? _picture = picture is null ? null : new(picture);

            /// <summary>Whether <paramref name="now" /> is the very picture answered then — or none, where none was.</summary>
            public bool Is(Picture? now) =>
                _picture is null
                    ? now is null
                    : now is not null && _picture.TryGetTarget(out var then) && ReferenceEquals(then, now);
        }
    }
}
