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
    /// <param name="hideDrawnCaption">
    ///     Whether a picture's caption hides once the picture is actually drawn (#71) — the reader's
    ///     <c>hide_drawn_caption</c> preference.
    /// </param>
    public static IReadOnlyList<Line> Feed(
        Post post,
        int width,
        bool revealed,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var shown = post.Boosted ?? post;

        return Parts([
            [
                .. Boosted(post, $"{post.Author} boosted", width),
                .. Answering(shown, width),
                .. Byline(shown, width, now, pictures),
            ],
            Body(shown, width, revealed),
            .. Media(shown, width, pictures, Inset.FeedRows, hideDrawnCaption),
            Poll(shown, width),
            [Counts(shown, spelledOut: false)],
        ]);
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
        IPictures? pictures = null,
        bool hideDrawnCaption = false)
    {
        var shown = post.Boosted ?? post;
        var avatar = Avatar.Of(shown, pictures);
        var room = Math.Max(0, width - avatar.Columns);

        return Parts([
            [
                .. Boosted(post, $"boosted by {post.Author}", width),
                .. Answering(shown, width),

                // Three byline rows rather than the feed's two, the third being the moment said exactly rather than as
                // an age — and stepped in with the other two though the avatar's box has run out below it, because two
                // rows starting in one column and a third starting in another would read as two things.
                .. avatar.Across(
                    Line.Of(TextWrap.Clip(shown.Author, room), Role.BylineName),
                    Line.Of(TextWrap.Clip($"@{shown.Account}", room), Role.BylineHandle),
                    Line.Of([
                        new Span(Elapsed.Moment(shown.PostedAt), Role.Muted),
                        new Span(" · ", Role.Muted),
                        new Span(
                            $"{Audience(shown.Visibility)} {PostVisibilityName.Of(shown.Visibility)}",
                            Role.Audience),
                    ])),
            ],
            Body(shown, width, revealed),
            .. Media(shown, width, pictures, Inset.WholeRows, hideDrawnCaption),
            Poll(shown, width),
            [Counts(shown, spelledOut: true)],
        ]);
    }

    /// <summary>
    ///     The parts of a post one after another, with a blank row between each — and none before the first, after the
    ///     last, or around a part there is nothing in.
    /// </summary>
    /// <remarks>
    ///     What keeps a post reading as a few things rather than one wall of ink. Said once here rather than as a
    ///     <see cref="Line.Blank" /> at each of the seams, because the seams are what kept being missed: the blank
    ///     between the byline and the body and the one ahead of the counts were put in by hand and the ones around the
    ///     attachments were not, so a post with two pictures ran its body into the first caption and the first
    ///     picture's last row into the second caption.
    ///     <para>
    ///         Each attachment is a part of its own, which is what puts a row between one picture and the next one's
    ///         caption. It costs one row per attachment, and only on a post that carries any — which is most of the
    ///         reason it is affordable at all, most posts being text.
    ///     </para>
    ///     <para>
    ///         An empty part is skipped rather than separated, so a post with nothing but a picture does not open on
    ///         two blank rows where its text would have been.
    ///     </para>
    /// </remarks>
    private static IReadOnlyList<Line> Parts(IEnumerable<IEnumerable<Line>> parts)
    {
        var lines = new List<Line>();

        foreach (var part in parts)
        {
            var rows = part.ToList();

            if (rows.Count == 0)
            {
                continue;
            }

            if (lines.Count > 0)
            {
                lines.Add(Line.Blank);
            }

            lines.AddRange(rows);
        }

        return lines;
    }

    /// <summary>
    ///     Who boosted this, where somebody did — <paramref name="said" /> because a feed names the booster first and
    ///     a post screen puts them after the fact, and the role turns on whether the booster was the reader.
    /// </summary>
    private static IEnumerable<Line> Boosted(Post post, string said, int width)
    {
        if (post.Boosted is not null)
        {
            yield return Line.Of([
                new Span("↺ ", post.Marks.Boosted ? Role.BoostMine : Role.Boost),
                new Span(TextWrap.Clip(said, width - 2), Role.Muted),
            ]);
        }
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
        var avatar = Avatar.Of(post, pictures);
        var room = Math.Max(0, width - avatar.Columns);

        var tail = $"{Audience(post.Visibility)} {Elapsed.Since(post.PostedAt, now)}";
        var name = TextWrap.Clip(post.Author, Math.Max(0, room - tail.Length - 1));

        return
        [
            .. avatar.Across(
                Line.Of([
                    new Span(name, Role.BylineName),
                    new Span(new string(' ', Math.Max(1, room - name.Length - tail.Length)), Role.Body),
                    new Span(tail, Role.Audience),
                ]),
                Line.Of(TextWrap.Clip($"@{post.Account}", room), Role.BylineHandle)),
        ];
    }

    /// <summary>
    ///     The author's avatar as a byline spends it: the columns it takes, the picture to send for, and the box to
    ///     draw it in once the pixels are here.
    /// </summary>
    /// <remarks>
    ///     Nothing at all where this client can already tell no avatar is coming — a terminal offering neither sixel
    ///     nor the Kitty graphics protocol, or an instance that named no avatar for this account. Five of sixty-one
    ///     columns is too much to spend holding a space open for a picture that will never fill it (ADR-0016). An
    ///     avatar that was named and then could not be fetched keeps its columns, because the two answers
    ///     <see cref="IPictures.Of" /> gives — not here yet, and never coming — are the same answer.
    ///     <para>
    ///         Where one <em>is</em> coming the columns are taken from the first frame, before the pixels land, which
    ///         is the opposite of what <see cref="Media" /> does with an attachment: an attachment's box appears under
    ///         its description and pushes nothing sideways, where a byline that gained five columns on arrival would
    ///         shove the name across the row as the reader was reading it.
    ///     </para>
    /// </remarks>
    /// <param name="Wanted">The avatar to send for, or <see langword="null" /> where none is drawn.</param>
    /// <param name="Box">Where to draw it, or <see langword="null" /> while the pixels are still on their way.</param>
    private readonly record struct Avatar(Drawn? Wanted, Inset? Box)
    {
        /// <summary>How many columns it costs: the picture and the gap after it, or none.</summary>
        public int Columns => Wanted is null ? 0 : AvatarColumns + 1;

        /// <summary>What <paramref name="post" />'s byline has to spend on its author's face.</summary>
        public static Avatar Of(Post post, IPictures? pictures)
        {
            if (pictures?.Cell is null || post.AvatarUrl is not { } url)
            {
                return default;
            }

            var wanted = Drawn.Avatar(post.Account, url);

            return new Avatar(
                wanted,
                pictures.Of(wanted) is null ? null : new Inset(wanted, Column: 0, AvatarColumns, AvatarRows));
        }

        /// <summary><paramref name="rows" /> stepped in to sit beside the avatar, in the order they were given.</summary>
        /// <remarks>
        ///     Asked for the whole byline at once rather than a row at a time, so that which row carries the box is
        ///     settled here instead of at each of the two callers — a feed and a post screen laying out different
        ///     bylines is the point, and their laying them out differently is what #62 spent its budget preventing.
        ///     <para>
        ///         The box goes on the first row and on none of the rest: a band is named once, at its top.
        ///         <see cref="Line.After" /> is what shifts it along with everything else, so the gutter and the
        ///         picture cannot come to disagree about which column they are in.
        ///     </para>
        /// </remarks>
        public IEnumerable<Line> Across(params Line[] rows)
        {
            if (Wanted is not { } wanted)
            {
                return rows;
            }

            var box = Box;

            return rows.Select((row, at) =>
            {
                var beside = row.After(
                    new Span(new string(' ', AvatarColumns), Role.Media),
                    new Span(" ", Role.Body));

                return at > 0 ? beside : beside with { Insets = box is null ? [] : [box], Wants = wanted };
            });
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

        // Nothing at all rather than the one empty row wrapping an empty string gives back. A post whose whole content
        // is a picture has no words, and a blank row standing in for them is a part of the post that is not there —
        // which Parts would then space off on both sides. An author's own blank line inside a post they wrote that way
        // is a different thing and is kept.
        if (string.IsNullOrWhiteSpace(post.Content))
        {
            return lines;
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
    /// <param name="hideDrawnCaption">
    ///     Whether the description drops once a picture is actually drawn under it (#71). A picture still on its way
    ///     keeps its description regardless — that is the whole of what a reader has while the pixels are not here yet,
    ///     and hiding it would be an arrival flicker rather than a quieter post.
    /// </param>
    /// <returns>
    ///     One run of rows per attachment, rather than one run for all of them, so that <see cref="Parts" /> puts a
    ///     blank row between each — which is the row between one picture and the next one's caption.
    /// </returns>
    private static IEnumerable<IReadOnlyList<Line>> Media(
        Post post,
        int width,
        IPictures? pictures,
        int mostRows,
        bool hideDrawnCaption)
    {
        foreach (var attached in post.Media)
        {
            if (!attached.IsDrawable || pictures?.Cell is not { } cell)
            {
                yield return [.. Linked(attached, width)];

                continue;
            }

            // The description first, so it does not move when the picture lands underneath it — and marked as wanting
            // a picture, which is how the view knows to send for one if this post is near enough to the screen.
            var drawn = Drawn.Attached(attached);
            var described = Described(attached, MediaMark, width) with { Wants = drawn };

            if (pictures.Of(drawn) is not { } picture || Inset.For(drawn, picture, cell, width, mostRows) is not { } inset)
            {
                yield return [described];

                continue;
            }

            yield return hideDrawnCaption ? [.. Box(inset)] : [described, .. Box(inset)];
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
    ///     A poll in full, on both the feed and the post screen alike: one row per option — a block bar carrying
    ///     <see cref="Role.Poll" />, the share and raw count beside it, and a leading <c>✓ </c> on the option this
    ///     profile picked — followed by whether and when it closes, a note where more than one answer may be chosen,
    ///     and the vote count. Nothing at all for a post with no poll.
    /// </summary>
    private static IEnumerable<Line> Poll(Post post, int width)
    {
        if (post.Poll is not { } poll)
        {
            yield break;
        }

        foreach (var option in poll.Options)
        {
            yield return PollOptionLine(poll, option, width);
        }

        if (poll.Closed)
        {
            yield return Line.Of(TextWrap.Clip("Closed", width), Role.Muted);
        }
        else if (poll.ExpiresAt is { } expires)
        {
            yield return Line.Of(TextWrap.Clip($"Closes {Elapsed.Moment(expires)}", width), Role.Muted);
        }

        if (poll.MultipleChoice)
        {
            yield return Line.Of(TextWrap.Clip("Choose as many as you like", width), Role.Muted);
        }

        yield return Line.Of(TextWrap.Clip(VoteCountText(poll), width), Role.Muted);
    }

    /// <summary>
    ///     One option's row: a leading <c>✓ </c> where this profile picked it, then a <c>▓</c>/<c>░</c> bar sized to
    ///     the share of the vote it drew, the percentage and raw count, and the option's own text — all in
    ///     <see cref="Role.Poll" />. An option whose count is withheld draws no bar at all rather than one guessed at,
    ///     which is what tells it apart from a genuinely unvoted option's empty, <c>0%</c> bar.
    /// </summary>
    private static Line PollOptionLine(PostPoll poll, PostPollOption option, int width)
    {
        var mark = option.Picked ? "✓ " : "  ";

        if (option.Votes is not { } votes)
        {
            return Line.Of(TextWrap.Clip($"{mark}{option.Text}", width), Role.Poll);
        }

        var percent = PollBar.PercentOf(poll, votes);

        return Line.Of(
            TextWrap.Clip($"{mark}{PollBar.Of(percent)} {percent}% ({Number.Of(votes)})  {option.Text}", width),
            Role.Poll);
    }

    /// <summary>How many votes the poll has drawn, and from how many accounts where that can differ from the count.</summary>
    private static string VoteCountText(PostPoll poll) => poll.MultipleChoice && poll.Voters is { } voters
        ? $"{Number.Of(poll.Votes)} votes from {Number.Of(voters)} accounts"
        : $"{Number.Of(poll.Votes)} votes";

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
