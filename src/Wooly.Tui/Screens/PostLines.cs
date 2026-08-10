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

    /// <summary>
    ///     How many columns an author's avatar takes, beside the two rows of their byline — four wide and two tall
    ///     being about square in a cell twice as tall as it is wide, which is the shape an avatar is.
    /// </summary>
    /// <remarks>
    ///     Fixed rather than worked out from the picture's own proportions the way an attachment's box is
    ///     (<see cref="Inset.For" />), because this box is not sized to a picture: it is a slot in a byline, and a
    ///     byline whose height depended on how square somebody's avatar happened to be would be a feed whose rows
    ///     moved as it scrolled.
    /// </remarks>
    private const int AvatarColumns = 4;

    /// <summary>How many rows the avatar stands beside, which is the two the byline takes.</summary>
    private const int AvatarRows = 2;

    /// <summary>
    ///     One post as a feed shows it: what it boosts and what it answers, a two-row byline with the author's avatar
    ///     beside it, the text, what is attached, and the three counts behind a blank of their own.
    /// </summary>
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

        lines.AddRange(Answering(shown, width));
        lines.AddRange(Byline(shown, width, now, pictures));
        lines.AddRange(Body(shown, width, revealed));
        lines.AddRange(Media(shown, width, pictures, Inset.FeedRows));

        // A blank ahead of the counts, so the three marks read as a footer rather than as one more line of the post
        // — which is how they read with nothing between them and the body (#62).
        lines.Add(Line.Blank);
        lines.Add(Counts(shown, spelledOut: false));

        return lines;
    }

    /// <summary>
    ///     The post whole, as the screen you drilled into: the same content and the same rows above the byline, with
    ///     the moment said exactly rather than as an age and the counts spelled out.
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
        var lines = new List<Line>();

        if (post.Boosted is not null)
        {
            lines.Add(Line.Of([
                new Span("↺ ", post.Marks.Boosted ? Role.BoostMine : Role.Boost),
                new Span(TextWrap.Clip($"boosted by {post.Author}", width - 2), Role.Muted),
            ]));
        }

        lines.AddRange(Answering(shown, width));

        var avatar = Portrait.Of(shown, pictures);
        var room = Math.Max(0, width - avatar.Columns);

        lines.Add(avatar.Beside(Line.Of(TextWrap.Clip(shown.Author, room), Role.BylineName), top: true));
        lines.Add(avatar.Beside(Line.Of(TextWrap.Clip($"@{shown.Account}", room), Role.BylineHandle)));

        // The moment said exactly rather than as an age. Stepped in with the two rows above it though the avatar's box
        // has run out: three rows starting in one column and a fourth starting in another would read as two things.
        lines.Add(avatar.Beside(Line.Of([
            new Span(Elapsed.Moment(shown.PostedAt), Role.Muted),
            new Span(" · ", Role.Muted),
            new Span($"{Audience(shown.Visibility)} {PostVisibilityName.Of(shown.Visibility)}", Role.Audience),
        ])));

        lines.Add(Line.Blank);
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
    ///     What a reply answers, on the row above the byline — or nothing at all, for a post that answers nothing.
    /// </summary>
    /// <remarks>
    ///     Worded by <see cref="PostReplyName" /> rather than here, because the CLI's post report says the same thing
    ///     about the same post and the two saying it differently is the whole reason that lives in <c>Wooly.Core</c>
    ///     (#63).
    ///     <para>
    ///         The slot the boost row already owns, and under it where a post is both: boost first, then this, then
    ///         the byline, so the two rows above a byline are always in the same two places. All
    ///         <see cref="Role.Muted" /> — it says which post this is, not what the post says.
    ///     </para>
    /// </remarks>
    private static IEnumerable<Line> Answering(Post post, int width)
    {
        if (PostReplyName.Of(post) is { } mark)
        {
            yield return Line.Of(TextWrap.Clip(mark, width), Role.Muted);
        }
    }

    /// <summary>
    ///     Two rows: the name with the audience and how long ago pushed to the right of it, then the handle — with the
    ///     author's avatar standing beside both. The right-hand pair is laid out first and the name gets whatever is
    ///     left, because a name is the thing there is most of and the least harm in cutting.
    /// </summary>
    /// <remarks>
    ///     One row until #62, where a feed of short posts turned out to read as one undifferentiated column of text
    ///     and the byline was the whole of what said where a post began. Costs two rows — the split, plus the blank
    ///     the two-row shape wants ahead of the body — and five columns, spent on these rows alone: the body, the
    ///     media and the counts stay full width, because this is a byline with a picture in it rather than an indent.
    /// </remarks>
    private static IEnumerable<Line> Byline(Post post, int width, DateTimeOffset now, IPictures? pictures)
    {
        var avatar = Portrait.Of(post, pictures);
        var room = Math.Max(0, width - avatar.Columns);

        var tail = $"{Audience(post.Visibility)} {Elapsed.Since(post.PostedAt, now)}";
        var name = TextWrap.Clip(post.Author, Math.Max(0, room - tail.Length - 1));

        yield return avatar.Beside(
            Line.Of([
                new Span(name, Role.BylineName),
                new Span(new string(' ', Math.Max(1, room - name.Length - tail.Length)), Role.Body),
                new Span(tail, Role.Audience),
            ]),
            top: true);

        yield return avatar.Beside(Line.Of(TextWrap.Clip($"@{post.Account}", room), Role.BylineHandle));

        yield return Line.Blank;
    }

    /// <summary>
    ///     The author's avatar as a byline spends it: the columns it takes, the picture to send for, and the box to
    ///     draw it in once the pixels are here.
    /// </summary>
    /// <remarks>
    ///     Nothing at all where no avatar will ever appear — a terminal offering neither sixel nor the Kitty graphics
    ///     protocol, or an instance that named no avatar for this account. Five of sixty-one columns is too much to
    ///     spend holding a space open for a picture that is never coming (ADR-0016).
    ///     <para>
    ///         Where one <em>is</em> coming the columns are taken from the first frame, before the pixels land, which
    ///         is the opposite of what <see cref="Media" /> does with an attachment: an attachment's box appears under
    ///         its description and pushes nothing sideways, where a byline that gained five columns on arrival would
    ///         shove the name across the row as the reader was reading it.
    ///     </para>
    /// </remarks>
    /// <param name="Wants">The avatar to send for, or <see langword="null" /> where none is drawn.</param>
    /// <param name="Box">Where to draw it, or <see langword="null" /> while the pixels are still on their way.</param>
    private readonly record struct Portrait(Drawn? Wants, Inset? Box)
    {
        /// <summary>How many columns it costs: the picture and the gap after it, or none.</summary>
        public int Columns => Wants is null ? 0 : AvatarColumns + 1;

        /// <summary>What <paramref name="post" />'s byline has to spend on its author's face.</summary>
        public static Portrait Of(Post post, IPictures? pictures)
        {
            if (pictures?.Cell is null || post.AvatarUrl is not { } url)
            {
                return default;
            }

            var avatar = Drawn.Avatar(post.Account, url);

            return new Portrait(
                avatar,
                pictures.Of(avatar) is null ? null : new Inset(avatar, Column: 0, AvatarColumns, AvatarRows));
        }

        /// <summary><paramref name="line" /> moved along to sit beside the avatar.</summary>
        /// <remarks>
        ///     The picture is named on the upper of the two rows and on neither where there is no avatar: a band is
        ///     named once, at its top. <see cref="Line.After" /> is what shifts it along with everything else, so the
        ///     gutter and the picture cannot come to disagree about which column they are in.
        /// </remarks>
        /// <param name="line">The row to move along.</param>
        /// <param name="top">Whether this is the upper row — where the box is named and the picture sent for.</param>
        public Line Beside(Line line, bool top = false)
        {
            if (Wants is null)
            {
                return line;
            }

            var beside = line.After(
                new Span(new string(' ', AvatarColumns), Role.Media),
                new Span(" ", Role.Body));

            return top ? beside with { Insets = Box is null ? [] : [Box], Wants = Wants } : beside;
        }
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

        // Wrapped first and split afterwards, one row at a time: the one place a post's text is drawn, so a tag, a
        // mention and an address take their own roles on a feed, inside a post, in a conversation and in the
        // notification list from this one line (#46).
        lines.AddRange(TextWrap.Wrap(post.Content, width).Select(row => new Line(BodyText.Spans(row))));

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

            // The description first, so it does not move when the picture lands underneath it — and marked as wanting
            // a picture, which is how the view knows to send for one if this post is near enough to the screen.
            var drawn = Drawn.Attached(attached);

            yield return Described(attached, MediaMark, width) with { Wants = drawn };

            if (pictures.Of(drawn) is { } picture
                && Inset.For(drawn, picture, cell, width, mostRows) is { } inset)
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
