using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A post as rows of spans: once for a feed, where it is one item among many, and once whole, where it is the
///     screen. Both name roles and neither knows what a colour is (ADR-0014).
///     <para>
///         Every state here has a glyph before it has a colour — <c>○ ◌ ● ✉</c> for the four audiences, <c>⚠</c> for a
///         warning, <c>↺</c> and <c>★</c> for the two marks, <c>▒▒▒▒</c> for a picture, <c>⏵</c> for an attachment
///         that is linked rather than drawn. That is not decoration: on a terminal reporting no colour, and to a reader
///         who cannot tell this green from that grey, the glyphs are the whole of what is being said.
///     </para>
/// </summary>
public static class PostLines
{
    /// <summary>
    ///     What marks a picture being drawn in place: it stands above the box, and is the whole of what a reader has
    ///     while the pixels are still on their way.
    /// </summary>
    private const string MediaMark = "▒▒▒▒";

    /// <summary>What stands in front of an attachment that is linked rather than drawn.</summary>
    private const string LinkMark = "⏵";

    /// <summary>One post as a feed shows it: a byline, the text, what is attached, and the three counts.</summary>
    /// <param name="post">The post, which may be a boost of another one.</param>
    /// <param name="width">How many columns there are, which at an 80-column terminal is 61.</param>
    /// <param name="revealed">Whether the reader has asked to see past a content warning.</param>
    /// <param name="now">What to measure the timestamp against.</param>
    /// <param name="pictures">
    ///     What this terminal can draw and what has arrived, or <see langword="null" /> where nothing is drawn — which
    ///     is what every attachment falls back to being linked means.
    /// </param>
    public static IReadOnlyList<Line> Feed(
        Post post,
        int width,
        bool revealed,
        DateTimeOffset now,
        IPictures? pictures = null)
    {
        var lines = new List<Line>();
        var shown = post.Boosted ?? post;

        if (post.Boosted is not null)
        {
            lines.Add(Line.Of([
                new Span("↺ ", post.Marks.Boosted ? Role.BoostMine : Role.Boost),
                new Span(TextWrap.Clip($"{post.Author} boosted", width - 2), Role.Muted),
            ]));
        }

        lines.Add(Byline(shown, width, now));
        lines.AddRange(Body(shown, width, revealed));
        lines.AddRange(Media(shown, width, pictures, Inset.FeedRows));
        lines.Add(Counts(shown, spelledOut: false));

        return lines;
    }

    /// <summary>
    ///     The post whole, as the screen you drilled into: the same content, with the byline broken across two rows
    ///     and the moment said exactly rather than as an age.
    /// </summary>
    /// <inheritdoc cref="Feed" />
    public static IReadOnlyList<Line> Whole(
        Post post,
        int width,
        bool revealed,
        DateTimeOffset now,
        IPictures? pictures = null)
    {
        var shown = post.Boosted ?? post;

        var lines = new List<Line>
        {
            Line.Of(TextWrap.Clip(shown.Author, width), Role.BylineName),
            Line.Of(TextWrap.Clip($"@{shown.Account}", width), Role.BylineHandle),
            Line.Of([
                new Span(Elapsed.Moment(shown.PostedAt), Role.Muted),
                new Span(" · ", Role.Muted),
                new Span($"{Audience(shown.Visibility)} {PostVisibilityName.Of(shown.Visibility)}", Role.Audience),
            ]),
            Line.Blank,
        };

        if (post.Boosted is not null)
        {
            lines.Insert(0, Line.Of([
                new Span("↺ ", post.Marks.Boosted ? Role.BoostMine : Role.Boost),
                new Span(TextWrap.Clip($"boosted by {post.Author}", width - 2), Role.Muted),
            ]));
        }

        lines.AddRange(Body(shown, width, revealed));
        lines.AddRange(Media(shown, width, pictures, Inset.WholeRows));
        lines.Add(Line.Blank);
        lines.Add(Counts(shown, spelledOut: true));

        return lines;
    }

    /// <summary>The mark for who can see a post, which says it where colour cannot.</summary>
    public static string Audience(PostVisibility visibility) => visibility switch
    {
        PostVisibility.Public => "○",
        PostVisibility.Unlisted => "◌",
        PostVisibility.Private => "●",
        PostVisibility.Direct => "✉",
        _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Not an audience this client draws."),
    };

    /// <summary>
    ///     The name, the handle, and — pushed to the right — the audience and how long ago. The right-hand pair is laid
    ///     out first and the byline gets whatever is left, because a name is the thing there is most of and the least
    ///     harm in cutting.
    /// </summary>
    private static Line Byline(Post post, int width, DateTimeOffset now)
    {
        var age = Elapsed.Since(post.PostedAt, now);
        var tail = $"{Audience(post.Visibility)} {age}";
        var room = Math.Max(0, width - tail.Length - 1);

        var name = TextWrap.Clip(post.Author, room);
        var handle = TextWrap.Clip($"@{post.Account}", Math.Max(0, room - name.Length - 1));

        var used = name.Length + (handle.Length > 0 ? handle.Length + 1 : 0);

        return Line.Of([
            new Span(name, Role.BylineName),
            new Span(handle.Length > 0 ? $" {handle}" : string.Empty, Role.BylineHandle),
            new Span(new string(' ', Math.Max(1, width - used - tail.Length)), Role.Body),
            new Span(tail, Role.Audience),
        ]);
    }

    /// <summary>
    ///     The text, or the warning standing in front of it. A warning is honoured rather than printed past: the
    ///     author put the post behind it, and a client that showed both would have made the warning pointless.
    /// </summary>
    private static IEnumerable<Line> Body(Post post, int width, bool revealed)
    {
        if (post.ContentWarning is { } warning && !revealed)
        {
            return
            [
                Line.Of([
                    new Span("⚠ ", Role.ContentWarning),
                    new Span(TextWrap.Clip(warning, width - 2), Role.ContentWarning),
                ]),
                Line.Of("x  show it", Role.Muted),
            ];
        }

        var lines = new List<Line>();

        if (post.ContentWarning is { } shown)
        {
            lines.Add(Line.Of([
                new Span("⚠ ", Role.ContentWarning),
                new Span(TextWrap.Clip(shown, width - 2), Role.ContentWarning),
            ]));
        }

        lines.AddRange(TextWrap.Wrap(post.Content, width).Select(row => Line.Of(row, Role.Body)));

        return lines;
    }

    /// <summary>
    ///     What is attached: each one behind a mark saying what it shows, and then either the picture itself drawn in
    ///     place or the address to reach it at.
    /// </summary>
    /// <remarks>
    ///     Three cases, and which one an attachment falls into is settled here rather than at the view, because it
    ///     changes how many rows the post takes. A picture this terminal can draw and has the pixels for gets a box; a
    ///     picture whose pixels have not landed yet gets its description and nothing else, so the rows appear under it
    ///     when they arrive rather than a hole opening above; and everything else — a video, a sound, an attachment of
    ///     a kind this client has no word for, and <em>any</em> attachment on a terminal offering neither sixel nor the
    ///     Kitty graphics protocol — gets the link and description the CLI gives it. There is no cell-by-cell fallback:
    ///     a photograph reduced to one coloured block per cell is not a picture of anything (ADR-0016).
    /// </remarks>
    /// <param name="pictures">What can be drawn and what is here, or <see langword="null" /> where nothing can be.</param>
    /// <param name="mostRows">The most rows a picture may take, which is what a feed and a whole post differ on.</param>
    private static IEnumerable<Line> Media(Post post, int width, IPictures? pictures, int mostRows)
    {
        foreach (var attached in post.Media)
        {
            if (!attached.IsDrawable || pictures?.Cell is not { } cell)
            {
                foreach (var line in Linked(attached, width))
                {
                    yield return line;
                }

                continue;
            }

            // The description first, so it does not move when the picture lands underneath it.
            yield return Described(attached, MediaMark, width);

            if (pictures.Of(attached) is { } picture
                && Inset.For(attached, picture, cell, width, mostRows) is { } inset)
            {
                foreach (var line in Box(inset))
                {
                    yield return line;
                }
            }
        }
    }

    /// <summary>An attachment that is not being drawn: what it shows, and where to get it.</summary>
    private static IEnumerable<Line> Linked(PostMedia attached, int width)
    {
        yield return Described(attached, LinkMark, width);

        // The address on rows of its own, wrapped rather than clipped: at 61 columns a real attachment address is
        // longer than the row, and a link with its end cut off is a link nobody can follow. Indented under the mark,
        // so a reader can see where it starts and where it stops.
        foreach (var row in TextWrap.Wrap(attached.Url, Math.Max(1, width - 2)))
        {
            yield return Line.Of($"  {row}", Role.Muted);
        }
    }

    /// <summary>
    ///     The rows a picture is drawn over. The first carries the box; the rest are rows of the screen the box covers,
    ///     and they are rows of the post so that everything below the picture is where the picture leaves it.
    /// </summary>
    private static IEnumerable<Line> Box(Inset inset)
    {
        yield return new Line([new Span(new string(' ', inset.Columns), Role.Media)]) { Insets = [inset] };

        for (var row = 1; row < inset.Rows; row++)
        {
            yield return Line.Blank;
        }
    }

    /// <summary>One attachment behind <paramref name="mark" />, saying what its author said it shows.</summary>
    private static Line Described(PostMedia attached, string mark, int width) => Line.Of([
        new Span($"{mark} ", Role.Media),
        new Span(
            TextWrap.Clip(attached.Shows, width - mark.Length - 1),
            attached.Description is null ? Role.Muted : Role.Media),
    ]);

    /// <summary>
    ///     The three counts. Each takes the role that says whether this profile is one of the accounts in it, which is
    ///     the whole reason a post carries the reader's own marks.
    /// </summary>
    /// <param name="spelledOut">
    ///     Whether each count says what it counts. A feed has room for the glyph and the number and a reader scanning
    ///     one does not need the word; the post screen has room for both.
    /// </param>
    private static Line Counts(Post post, bool spelledOut) => Line.Of([
        new Span($"↺ {Number.Of(post.Boosts)}{Word(" boosts", spelledOut)}", post.Marks.Boosted ? Role.BoostMine : Role.Boost),
        new Span("   ", Role.Muted),
        new Span(
            $"★ {Number.Of(post.Favorites)}{Word(" favorites", spelledOut)}",
            post.Marks.Favorited ? Role.FavoriteMine : Role.Favorite),
        new Span("   ", Role.Muted),
        new Span($"↩ {Number.Of(post.Replies)}{Word(" replies", spelledOut)}", Role.Muted),
        new Span(post.Marks.Pinned ? "   pinned" : string.Empty, Role.Muted),
    ]);

    private static string Word(string word, bool spelledOut) => spelledOut ? word : string.Empty;
}
