using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;

namespace Wooly.Tui.Screens;

/// <summary>
///     Each post's feed rows, stamped, kept from one frame to the next and laid out again only where something they are
///     drawn from has changed (#584). A frame laid out every post on its screen afresh, so a scroll slowed as the feed
///     grew: 12 ms a frame over 40 posts, 52 over 800, and most of it the garbage of rows nobody could see.
/// </summary>
/// <remarks>
///     A post's rows are drawn from:
///     <list type="bullet">
///         <item>the post — the same one, since a mark, an edit or a vote hands the list a new one;</item>
///         <item>its ordinal on the screen, whether it is picked, and the room it is drawn in;</item>
///         <item>what its reader has done to it (<see cref="Reading" />), its poll's choices by what they are;</item>
///         <item>
///             what the drawing says but for the clock, the page and the pictures: the Raster, the height, the caption
///             hidden or not, the blurs. No post's rows keep to the page (<see cref="Drawing.Top" />).
///         </item>
///         <item>the clock, only as far as how long ago the row says (<see cref="PostLines.FeedAge" />);</item>
///         <item>
///             the pictures, only as far as what laying it out asked of them: every question asked, and its answer, is
///             kept with the rows, and they are kept only while every one is answered the same. A picture landing,
///             refused or let go of lays out again just the posts that asked about it — and rows never hold on to a
///             picture the cache has let go of past the next frame.
///         </item>
///     </list>
/// </remarks>
internal sealed class KeptRows
{
    private readonly Dictionary<Post, Kept> _kept = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     <paramref name="post" />'s rows, stamped as the <paramref name="at" />th thing on the screen, picked out or
    ///     not: those kept from before where nothing they are drawn from has changed, and laid out afresh where it has.
    /// </summary>
    public IReadOnlyList<Line> Rows(Post post, int at, bool picked, Drawing drawing, Reading reading)
    {
        var room = drawing.In(Stamped.Room(drawing.Width));
        var drawnFrom = new DrawnFrom(
            at,
            picked,
            room with { Now = default, Top = 0, Pictures = null },
            reading.Revealed,
            reading.Reference,
            reading.Chosen is { } chosen ? [.. chosen.Order()] : null,
            PostLines.FeedAge(post, reading, drawing.Now));

        if (_kept.TryGetValue(post, out var kept) && kept.DrawnFrom.Same(drawnFrom) && kept.Asked.SameNow(drawing.Pictures))
        {
            return kept.Rows;
        }

        var asked = new Asked(drawing.Pictures);
        var rows = Stamped.Rows(PostLines.Feed(post, room with { Pictures = asked.Recording }, reading), at, picked);

        _kept[post] = new Kept(drawnFrom, asked, rows);

        return rows;
    }

    /// <summary>Lets go of the rows of every post but <paramref name="posts" />, once there are more of them kept.</summary>
    public void KeepOnly(IReadOnlyList<Post> posts)
    {
        if (_kept.Count <= posts.Count)
        {
            return;
        }

        var still = new HashSet<Post>(posts, ReferenceEqualityComparer.Instance);

        foreach (var gone in _kept.Keys.Where(post => !still.Contains(post)).ToList())
        {
            _kept.Remove(gone);
        }
    }

    private sealed record Kept(DrawnFrom DrawnFrom, Asked Asked, IReadOnlyList<Line> Rows);

    /// <summary>Everything a post's rows are drawn from but the post and the pictures.</summary>
    private sealed record DrawnFrom(
        int At,
        bool Picked,
        Drawing Drawing,
        bool Revealed,
        Reference? Reference,
        int[]? Chosen,
        string Age)
    {
        public bool Same(DrawnFrom other) =>
            At == other.At
            && Picked == other.Picked
            && Drawing == other.Drawing
            && Revealed == other.Revealed
            && Reference == other.Reference
            && (Chosen ?? []).SequenceEqual(other.Chosen ?? [])
            && (Chosen is null) == (other.Chosen is null)
            && Age == other.Age;
    }

    /// <summary>
    ///     What laying out one post asked of the pictures, and what it was answered: asked through
    ///     <see cref="Recording" />, which answers as the pictures it stands in front of do.
    /// </summary>
    private sealed class Asked(IPictures? pictures)
    {
        /// <summary>The pictures, as laying the post out asks them; none where there are none.</summary>
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
                if (!ReferenceEquals(now.Of(drawn), answer))
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
            public List<(Drawn Drawn, Picture? Answer)> Of { get; } = [];

            public List<(Drawn Drawn, Unshowable? Answer)> WhyNot { get; } = [];

            Picture? IPictures.Of(Drawn drawn)
            {
                var answer = pictures.Of(drawn);

                Of.Add((drawn, answer));

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
    }
}
