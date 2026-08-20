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
///         warning, <c>↺</c>/<c>⥀</c> and <c>☆</c>/<c>★</c> for whether a boost or favorite is the reader's own,
///         <c>▒▒▒▒</c> for a picture, <c>⏵</c> for what is walked to and opened rather than drawn — an attachment, and
///         a link preview's title. That is not decoration: on a terminal reporting no colour, and to a reader who
///         cannot tell this green from that grey, the glyphs are the whole of what is being said.
///     </para>
/// </summary>
public static class PostLines
{
    /// <summary>
    ///     What marks a picture being drawn in place: it stands above the box, and is the whole of what a reader has
    ///     while the pixels are still on their way.
    /// </summary>
    private const string MediaMark = "▒▒▒▒";

    /// <summary>
    ///     What stands in front of a row a reader walks to and opens: an attachment that is linked rather than drawn,
    ///     and a link preview's title (#116). One mark rather than two, because it says what <c>⏎</c> does here rather
    ///     than what kind of thing is on the other end of it.
    /// </summary>
    private const string LinkMark = "⏵";

    /// <summary>
    ///     What a post whose media the instance marked sensitive says where its attachments would have been, on a post
    ///     carrying no warning of its own to say it (#113).
    /// </summary>
    private const string SensitiveMedia = "Sensitive media";

    /// <summary>
    ///     How a reader asks past whichever of the two a post is hiding behind, said once so that the warning over a
    ///     post's text and the row standing in for its attachments cannot come to name different keys.
    /// </summary>
    private const string AskPastIt = "x  show it";

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
    /// <param name="reading">
    ///     What this reader has done to this post — asked past its warning, walked to a reference in it — which is
    ///     <see langword="default" /> for the posts nobody has touched (#95).
    /// </param>
    /// <param name="now">What to measure the timestamp against.</param>
    /// <param name="pictures">
    ///     What this terminal can draw and what has arrived, or <see langword="null" /> where nothing is drawn — which
    ///     is what every attachment falls back to being linked means.
    /// </param>
    /// <param name="hideDrawnCaption">
    ///     Whether a picture's caption hides once the picture is actually drawn (#71) — the reader's
    ///     <c>hide_drawn_caption</c> preference.
    /// </param>
    /// <param name="saysHowToAskPast">
    ///     Whether the <c>x  show it</c> row is drawn under whatever the post is hiding. On for every screen that picks
    ///     the post out, and off for exactly one that does not: the conversations list, where a row is a conversation
    ///     and <c>x</c> has no post to be asked about — so the row would be naming a key that cannot act, which reads
    ///     as a shell that missed the press (<c>docs/tui-shell.md</c>, #120). The warning itself is drawn either way.
    /// </param>
    public static IReadOnlyList<Line> Feed(
        Post post,
        int width,
        Reading reading,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false,
        bool saysHowToAskPast = true)
    {
        var shown = post.Boosted ?? post;

        return Parts([
            [
                .. Boosted(post, $"{post.Author} boosted", width),
                .. Answering(shown, width),
                .. Byline(shown, width, now, pictures),
            ],
            Body(shown, width, reading, saysHowToAskPast),
            .. Media(shown, width, pictures, Inset.FeedRows, hideDrawnCaption, reading, saysHowToAskPast),
            LinkPreview(shown, width, pictures, Inset.FeedRows, reading),
            Poll(shown, width, reading),
            [Counts(shown, spelledOut: false)],
        ]);
    }

    /// <summary>
    ///     The post whole, as the screen you drilled into: the same content and the same rows above the byline, with
    ///     the moment said exactly rather than as an age and the counts spelled out.
    /// </summary>
    /// <inheritdoc cref="Feed" />
    /// <param name="saysWhatItAnswers">
    ///     Whether the <c>↳</c> row above the byline is drawn. On for every post anywhere, and off for exactly one:
    ///     the post a post screen is about, whose ancestor chain is drawn whole above it instead (#86).
    /// </param>
    public static IReadOnlyList<Line> Whole(
        Post post,
        int width,
        Reading reading,
        DateTimeOffset now,
        IPictures? pictures = null,
        bool hideDrawnCaption = false,
        bool saysWhatItAnswers = true)
    {
        var shown = post.Boosted ?? post;
        var avatar = Avatar.Of(shown, pictures);
        var room = Math.Max(0, width - avatar.Columns);

        return Parts([
            [
                .. Boosted(post, $"boosted by {post.Author}", width),
                .. saysWhatItAnswers ? Answering(shown, width) : [],

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
            // The post screen is about this post, so x is always something that can act on it here.
            Body(shown, width, reading, saysHowToAskPast: true),
            .. Media(shown, width, pictures, Inset.WholeRows, hideDrawnCaption, reading, saysHowToAskPast: true),
            LinkPreview(shown, width, pictures, Inset.WholeRows, reading),
            Poll(shown, width, reading),
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
                new Span($"{BoostMark(post.Marks.Boosted)} ", post.Marks.Boosted ? Role.BoostMine : Role.Boost),
                new Span(TextWrap.Clip(said, width - 2), Role.Muted),
            ]);
        }
    }

    /// <summary>
    ///     The boost mark: the closed circle arrow where the boost is this profile's own, and the open one — its
    ///     ordinary reading — where it is anybody else's. The same rotation either way, so a reader is told whose
    ///     boost this is by whether the circle is open or shut rather than by which way it turns.
    /// </summary>
    private static string BoostMark(bool mine) => mine ? "⥀" : "↺";

    /// <summary>
    ///     The favorite mark: a filled star where this profile favorited the post, and a hollow one where nobody
    ///     reading it did or somebody else did without them.
    /// </summary>
    private static string FavoriteMark(bool mine) => mine ? "★" : "☆";

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
    /// <param name="reading">What the reader has done to this post: asked past its warning, picked a reference in it.</param>
    /// <param name="saysHowToAskPast">
    ///     Whether the row naming <c>x</c> goes under the warning. The text stays behind it either way — what is off on
    ///     the one screen that says no is the offer, not the hiding (#120).
    /// </param>
    private static IEnumerable<Line> Body(Post post, int width, Reading reading, bool saysHowToAskPast)
    {
        if (post.ContentWarning is { } warning && !reading.Revealed)
        {
            return Hiding(warning, width, saysHowToAskPast);
        }

        var lines = new List<Line>();

        if (post.ContentWarning is { } shown)
        {
            lines.Add(Warning(shown, width));
        }

        // Nothing at all rather than the one empty row wrapping an empty string gives back. A post whose whole content
        // is a picture has no words, and a blank row standing in for them is a part of the post that is not there —
        // which Parts would then space off on both sides. An author's own blank line inside a post they wrote that way
        // is a different thing and is kept.
        if (string.IsNullOrWhiteSpace(post.Content))
        {
            return lines;
        }

        // The references found once on the whole text and the rows sliced out of it afterwards: the one place a post's
        // text is drawn, so a tag, a mention and an address take their own roles on a feed, inside a post, in a
        // conversation and in the notification list from this one line (#46) — and an address the wrap cut in two is
        // still one reference, because what it is was settled before the cut (#83).
        // The picked one is passed in as itself rather than as a place in this list, so a pick left over from a post
        // that has since been edited matches nothing here — a pick on nothing rather than a bracket around whatever
        // is now written in that place.
        var references = BodyText.References(post.Content);

        lines.AddRange(TextWrap.Rows(post.Content, width)
                               .Select(row => new Line(BodyText.Spans(row, references, reading.Reference))));

        return lines;
    }

    /// <summary>
    ///     One warning, said the one way: the mark and what is being warned about. Written here rather than at each of
    ///     the three places one goes up — over a warned post's text, over the same text once it is shown, and where a
    ///     sensitive post's attachments are being kept — so a warning cannot come to look like two different things on
    ///     one post.
    /// </summary>
    private static Line Warning(string said, int width) => Line.Of([
        new Span("⚠ ", Role.ContentWarning),
        new Span(TextWrap.Clip(said, width - 2), Role.ContentWarning),
    ]);

    /// <summary>
    ///     What stands in place of what a post is hiding: the warning, and under it the row naming the key that would
    ///     show it — where there is a key that can act. Both of the things a post hides behind go up this way, so that
    ///     what asks and whether it is asked at all cannot come to differ between them (#120).
    /// </summary>
    private static Line[] Hiding(string said, int width, bool saysHowToAskPast) => saysHowToAskPast
        ? [Warning(said, width), Line.Of(AskPastIt, Role.Muted)]
        : [Warning(said, width)];

    /// <summary>
    ///     What is attached: a picture drawn in place or the address to reach an undrawn one at, and for everything
    ///     else — a video, an animation, a sound, or an attachment of a kind this client has no word for — the label
    ///     <c>←</c>/<c>→</c> walks to and <c>⏎</c> opens, with a video's or an animation's own preview drawn in a box
    ///     under it where the instance offered one (ADR-0017).
    /// </summary>
    /// <remarks>
    ///     Which case an attachment falls into is settled here rather than at the view, because it changes how many
    ///     rows the post takes. A picture this terminal can draw and has the pixels for gets a box; a picture whose
    ///     pixels have not landed yet, or that this terminal cannot draw at all, gets the link and description the CLI
    ///     already gives it (ADR-0016). There is no cell-by-cell fallback: a photograph reduced to one coloured block
    ///     per cell is not a picture of anything (ADR-0016).
    ///     <para>
    ///         The split is <see cref="PostMedia.Opens" /> rather than <see cref="PostMedia.IsDrawable" />, and the two
    ///         stopped being opposites in #110: a video is walked <em>and</em> drawn. What tells the two halves apart
    ///         is what the row above the box says — a picture's description, which the box stands in for, against a
    ///         video's label, which says what opening it would reach and is not something a still frame can stand in
    ///         for at all.
    ///     </para>
    /// </remarks>
    /// <param name="pictures">What can be drawn and what is here, or <see langword="null" /> where nothing can be.</param>
    /// <param name="mostRows">The most rows a picture may take, which is what a feed and a whole post differ on.</param>
    /// <param name="hideDrawnCaption">
    ///     Whether what an attachment says it shows drops once its pixels are actually drawn (#71) — a picture's own
    ///     caption, and since #110 a video's description under its label, on the same terms. Anything still on its way
    ///     keeps what it says regardless: that is the whole of what a reader has while the pixels are not here yet, and
    ///     hiding it would be an arrival flicker rather than a quieter post.
    /// </param>
    /// <param name="reading">
    ///     What this reader has done to this post, which is what says whether one of its attachment references is
    ///     picked out and drawn in brackets (#109).
    /// </param>
    /// <param name="saysHowToAskPast">
    ///     Whether the row naming <c>x</c> goes under the <c>⚠ Sensitive media</c> this prints where nothing else is
    ///     asking. Off on the screen where <c>x</c> has no post to act on, exactly as it is over a warning its author
    ///     wrote — the attachments are hidden there either way (#120).
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
        bool hideDrawnCaption,
        Reading reading,
        bool saysHowToAskPast)
    {
        // A warned post's attachments are part of what the warning covers, and nothing about one is drawn until the
        // reader has asked past it: no box, no label, no description, no address — and no Wants, which is what keeps
        // the pixels from being sent for at all (#113, ADR-0016's amendment).
        if (post.IsWarned && !reading.Revealed)
        {
            // Where the post's own warning is not already asking, this is: a photograph marked sensitive under no
            // warning at all would otherwise be hidden with nothing on screen to say a key meant anything. There is
            // always something to say it about here — a post warned by the flag alone is one carrying attachments or a
            // link preview, which is what Post.IsWarned settles. It stands above where both of them would be, and is
            // said here rather than once for each, because it is one post hiding things rather than a prompt per
            // thing (#113, #116).
            if (post.ContentWarning is null)
            {
                yield return Hiding(SensitiveMedia, width, saysHowToAskPast);
            }

            yield break;
        }

        // Built once, off the same formula Screen.References walks, so a bracket here and the pick the reader made
        // can never come to disagree about which attachment it landed on (AttachmentReferences).
        var references = AttachmentReferences.Of(post);
        var at = 0;

        foreach (var attached in post.Media)
        {
            if (attached.Opens)
            {
                yield return Opened(
                    attached,
                    references[at],
                    reading.Reference,
                    width,
                    pictures,
                    mostRows,
                    hideDrawnCaption);

                at++;

                continue;
            }

            if (pictures?.Cell is not { } cell)
            {
                yield return [.. LinkedImage(attached, width)];

                continue;
            }

            // The description first, so it does not move when the picture lands underneath it — and marked as wanting
            // a picture, which is how the view knows to send for one if this post is near enough to the screen.
            var drawn = Drawn.Attached(attached);
            var described = Described(attached, MediaMark, width) with { Wants = drawn };

            if (BoxFor(drawn, pictures, cell, width, mostRows) is not { } inset)
            {
                yield return [described];

                continue;
            }

            yield return hideDrawnCaption ? [.. Box(inset)] : [described, .. Box(inset)];
        }
    }

    /// <summary>
    ///     A picture that is not being drawn: what it shows, and where to get it. Unaffected by ADR-0017 — <c>Image</c>
    ///     never joins the walk, so this is exactly what the CLI already prints (ADR-0016).
    /// </summary>
    private static IEnumerable<Line> LinkedImage(PostMedia attached, int width)
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
    ///     An attachment opened rather than shown — a <c>Video</c>, <c>Animation</c>, <c>Audio</c> or <c>Unknown</c>:
    ///     its label, and under it the preview drawn in a box of its own where there is one and this terminal can draw
    ///     it (ADR-0017, #110).
    /// </summary>
    /// <remarks>
    ///     The label is the fixed point of all four cases: it is in the same row whether the preview is coming, has
    ///     landed, or was never going to, because it is what <c>⏎</c> acts on rather than a caption standing in for
    ///     something. Only the description under it moves, and only behind the same preference a picture's caption
    ///     already hides behind (#71) — what a box has landed and taken over saying.
    ///     <para>
    ///         A sound and an unknown kind never reach the box at all, however much cover art an instance sends with
    ///         them, and neither does a video the instance offered no preview of — both are
    ///         <see cref="PostMedia.IsDrawable" />'s answer rather than a case of their own here.
    ///     </para>
    /// </remarks>
    private static IReadOnlyList<Line> Opened(
        PostMedia attached,
        Reference reference,
        Reference? picked,
        int width,
        IPictures? pictures,
        int mostRows,
        bool hideDrawnCaption)
    {
        Line Label(bool saysWhatItShows) =>
            AttachmentReferenceLine(attached, reference, picked, width, saysWhatItShows);

        if (!attached.IsDrawable || pictures?.Cell is not { } cell)
        {
            return [Label(saysWhatItShows: true)];
        }

        // Marked as wanting the preview whether or not it is here yet, the way a picture's own description is: this
        // row is what the view reads to decide the pixels are worth sending for.
        var drawn = Drawn.Attached(attached);

        return BoxFor(drawn, pictures, cell, width, mostRows) is not { } inset
            ? [Label(saysWhatItShows: true) with { Wants = drawn }]
            : [Label(saysWhatItShows: !hideDrawnCaption) with { Wants = drawn }, .. Box(inset)];
    }

    /// <summary>
    ///     The box <paramref name="drawn" />'s pixels get, or <see langword="null" /> while they are not here — which
    ///     is the same answer as a picture that will never arrive, and deliberately so (ADR-0016).
    /// </summary>
    /// <remarks>
    ///     The one lookup-and-size a picture's own rows and a video's preview both go through, said once so the two
    ///     cannot come to size the same box differently. A lookup and nothing else: asking never sends for anything,
    ///     and only the view — the one thing that knows where the scroll has got to — may do that (ADR-0016).
    /// </remarks>
    private static Inset? BoxFor(Drawn drawn, IPictures pictures, CellSize cell, int width, int mostRows) =>
        pictures.Of(drawn) is { } picture ? Inset.For(drawn, picture, cell, width, mostRows) : null;

    /// <summary>
    ///     A <c>Video</c>, <c>Animation</c>, <c>Audio</c> or <c>Unknown</c> attachment's own row: the mark, its kind
    ///     capitalized — bracketed where <paramref name="picked" /> names this attachment — and its description
    ///     alongside where its author gave one and <paramref name="saysWhatItShows" /> (ADR-0017, #109).
    /// </summary>
    /// <remarks>
    ///     The address itself is never printed: it is what <c>⏎</c> now opens rather than what the row says, which is
    ///     the whole reason the raw rows <see cref="LinkedImage" /> still prints for a picture are gone from here.
    /// </remarks>
    /// <param name="saysWhatItShows">
    ///     Whether the author's description is written after the label. Off once a preview has landed under it and the
    ///     reader has asked for that (#71); the label itself is never off, so nothing on the row moves either way.
    /// </param>
    private static Line AttachmentReferenceLine(
        PostMedia attached,
        Reference reference,
        Reference? picked,
        int width,
        bool saysWhatItShows)
    {
        var spans = MarkAndLabel(Capitalized(MediaKindName.Written(attached.Kind)), picked == reference);

        if (saysWhatItShows && attached.Description is { } description)
        {
            var used = spans.Sum(span => span.Text.Length) + 1;

            spans.Add(new Span($" {TextWrap.Clip(description, Math.Max(0, width - used))}", Role.Media));
        }

        return new Line(spans);
    }

    /// <summary>
    ///     The front of a row <c>←</c>/<c>→</c> walks to: the mark and <paramref name="label" />, in the brackets a
    ///     picked reference is drawn in where <paramref name="bracketed" />. Handed back open, for a caller with more
    ///     to say on the row — an attachment's description follows it.
    /// </summary>
    /// <remarks>
    ///     Said once for an attachment's kind and a link preview's title, so the two cannot come to mark a pick
    ///     differently — the brackets are what a reader reads a pick off, on a terminal with no colour and to a reader
    ///     who cannot tell one colour from another (ADR-0014).
    /// </remarks>
    private static List<Span> MarkAndLabel(string label, bool bracketed)
    {
        var spans = new List<Span> { new($"{LinkMark} ", Role.Media) };

        if (bracketed)
        {
            spans.Add(new Span(BodyText.Opening, Role.ReferencePicked));
        }

        spans.Add(new Span(label, Role.Media));

        if (bracketed)
        {
            spans.Add(new Span(BodyText.Closing, Role.ReferencePicked));
        }

        return spans;
    }

    /// <summary>One word, its first letter upper-cased — <c>MediaKindName.Written</c>'s own spelling, said out loud.</summary>
    private static string Capitalized(string word) => $"{char.ToUpperInvariant(word[0])}{word[1..]}";

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
    ///     What the instance made of a link the author wrote into the post: its title on the row <c>←</c>/<c>→</c>
    ///     walks to and <c>⏎</c> opens, the site's name, the description and the page's own author under it, and the
    ///     picture the instance chose drawn in a box below where this terminal can draw one (ADR-0018).
    /// </summary>
    /// <remarks>
    ///     After everything the author attached, which is the order Mastodon's own web UI uses. Nothing in its docs
    ///     says a post carrying attachments is never sent a link preview too, so both are drawn and the order between
    ///     them is settled here rather than found out later.
    ///     <para>
    ///         Behind a warned post's warning exactly as an attachment is since #113: no title, no description, no box
    ///         and no <c>Wants</c>, which is what keeps the pixels from being sent for at all. Nothing is said in place
    ///         of it — a post hiding anything is already showing <see cref="Media" />'s prompt or its author's own
    ///         warning, and a second one under that would be the same offer made twice.
    ///     </para>
    ///     <para>
    ///         <c>hide_drawn_caption</c> is the one thing an attachment is asked about here and a link preview is not.
    ///         That preference drops what a picture says it shows once the picture itself is on screen saying it (#71);
    ///         a link preview's description is about the page rather than about the picture beside it, so a box landing
    ///         under the words does not stand in for them.
    ///     </para>
    ///     <para>
    ///         No address is printed on any of these rows, which is <see cref="Opened" />'s shape rather than
    ///         <see cref="LinkedImage" />'s: the address is what <c>⏎</c> hands the browser, and a picture that cannot
    ///         be drawn falls back to the words above it rather than to rows of a URL nobody typed. The raw rows an
    ///         undrawn <c>Image</c> still gets are for the one thing here that is <em>not</em> walked to (ADR-0017).
    ///         The CLI, which has no <c>⏎</c> to offer, prints it instead (ADR-0018, #117).
    ///     </para>
    /// </remarks>
    /// <param name="mostRows">The most rows the picture may take, which is what a feed and a whole post differ on.</param>
    private static IReadOnlyList<Line> LinkPreview(
        Post post,
        int width,
        IPictures? pictures,
        int mostRows,
        Reading reading)
    {
        if (post.LinkPreview is not { } link || (post.IsWarned && !reading.Revealed))
        {
            return [];
        }

        // Null on a terminal that draws nothing and where the instance chose no picture alike — the two answers that
        // read the same, which is what "linked rather than drawn" already means for an attachment (ADR-0016).
        var drawn = pictures?.Cell is null ? null : Drawn.LinkPreview(link);

        // The words first and always, so nothing a reader is looking at moves when the pixels land underneath them.
        // The walked row is what carries the Wants, the way an attachment's own description does.
        List<Line> lines =
        [
            LinkPreviewLine(link, LinkPreviewReference.Of(post), reading.Reference, width) with { Wants = drawn },
            .. LinkPreviewSays(link, width),
        ];

        if (drawn is not null
            && pictures?.Cell is { } cell
            && BoxFor(drawn, pictures, cell, width, mostRows) is { } inset)
        {
            lines.AddRange(Box(inset));
        }

        return lines;
    }

    /// <summary>
    ///     The link preview's own row: the mark and the page's title, bracketed where <paramref name="picked" />
    ///     names this link preview. Clipped rather than wrapped, the way an attachment's description is — a title is a
    ///     label on the thing <c>⏎</c> opens rather than something to be read to the end.
    /// </summary>
    /// <remarks>
    ///     What the row says is <see cref="LinkPreview.Name" />'s — the site's name standing in for a title the
    ///     instance made nothing of, and the address itself where it named neither, said once for both surfaces
    ///     (#125). There is always a row to walk to, because the address is the whole reason a link preview is drawn
    ///     at all (ADR-0018). The clipping and the brackets are this surface's own.
    /// </remarks>
    private static Line LinkPreviewLine(LinkPreview link, Reference? reference, Reference? picked, int width)
    {
        var bracketed = reference is not null && picked == reference;
        var room = width - LinkMark.Length - 1 - (bracketed ? BodyText.Opening.Length + BodyText.Closing.Length : 0);

        return new Line(MarkAndLabel(TextWrap.Clip(link.Name, Math.Max(0, room)), bracketed));
    }

    /// <summary>
    ///     What the instance said about the page, under the row that opens it and indented past the mark: the site, the
    ///     description, and who the page says wrote it — one row each, and nothing at all for whatever it did not say.
    /// </summary>
    /// <remarks>
    ///     Which rows there are and what they say is <see cref="LinkPreview.Says" />'s, so that the CLI writing the
    ///     same page cannot come to say something else about it (#125) — including the author's name as plain text
    ///     here and nowhere else, which is what keeps a post from carrying three things reaching for the same handful
    ///     of places (ADR-0018). What is left here is the indent, the clip and the role: all
    ///     <see cref="Role.Muted" />, because this says what is on the other end of the row above rather than what the
    ///     post says.
    /// </remarks>
    private static IEnumerable<Line> LinkPreviewSays(LinkPreview link, int width) =>
        link.Says.Select(row => Line.Of($"  {TextWrap.Clip(row, Math.Max(1, width - 2))}", Role.Muted));

    /// <summary>
    ///     A poll in full, on both the feed and the post screen alike: one row per option — a block bar carrying
    ///     <see cref="Role.Poll" />, the share and raw count beside it, and a leading <c>✓ </c> on the option this
    ///     profile picked — followed by whether and when it closes, a note where more than one answer may be chosen,
    ///     and the vote count. Nothing at all for a post with no poll.
    /// </summary>
    /// <remarks>
    ///     While the reader has a vote toggled and not yet cast, every option leads with a box instead of the mark:
    ///     the poll they are reading has become the ballot they are filling in, and a checked box on one row is only
    ///     legible against the empty ones beside it (<c>docs/tui-shell.md</c>, #87).
    ///     <para>
    ///         Behind the post's <em>content warning</em> on exactly the terms <see cref="Body" /> is, and behind the
    ///         instance's sensitive flag on none of them: a poll's answers are words its author typed, which is what
    ///         that warning is written about, while the flag is a mark over media and says nothing about words (#119).
    ///         That is the one place this parts company with <see cref="Media" /> and <see cref="LinkPreview" />, which
    ///         ask <see cref="Post.IsWarned" /> and so answer to either half.
    ///     </para>
    ///     <para>
    ///         Nothing is said in place of it. A post carrying a warning is already showing it and already naming
    ///         <c>x</c>, and a second prompt under that would be the same offer made twice — the reason
    ///         <see cref="Media" /> prints its own only where no warning is already asking (#113, #116).
    ///     </para>
    /// </remarks>
    private static IEnumerable<Line> Poll(Post post, int width, Reading reading)
    {
        if (post.Poll is not { } poll || (post.ContentWarning is not null && !reading.Revealed))
        {
            yield break;
        }

        // Only where something is actually toggled: an empty ballot is a poll nobody is voting in yet, and drawing
        // boxes down it would say a vote is being cast whenever a post with a poll on it is picked out.
        var chosen = reading.Chosen is { Count: > 0 } toggled ? toggled : null;

        for (var at = 0; at < poll.Options.Count; at++)
        {
            var option = poll.Options[at];

            var mark = chosen is null
                ? option.Picked ? "✓ " : "  "
                : chosen.Contains(at) ? "[x] " : "[ ] ";

            yield return PollOptionLine(poll, option, width, mark);
        }

        if (chosen is not null)
        {
            // Under the ballot rather than only on the status row, which is where the reader is not looking: they are
            // looking at the boxes they have just ticked, and this is the one moment in the shell where a key has to
            // be found rather than remembered. It costs a row, and only while a vote is standing uncast.
            yield return Line.Of(TextWrap.Clip("v casts this vote, esc discards it", width), Role.Muted);
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
    ///     One option's row: <paramref name="mark" />, then a <c>▓</c>/<c>░</c> bar sized to the share of the vote it
    ///     drew, the percentage and raw count, and the option's own text — all in <see cref="Role.Poll" />. An option
    ///     whose count is withheld draws no bar at all rather than one guessed at, which is what tells it apart from a
    ///     genuinely unvoted option's empty, <c>0%</c> bar.
    /// </summary>
    /// <param name="mark">
    ///     What the row leads with: <c>✓ </c> where this profile has voted for it, or the ballot's <c>[x] </c>/
    ///     <c>[ ] </c> while a vote is toggled and uncast. Worked out by <see cref="Poll" />, which is what knows
    ///     whether either applies — the option itself only knows what the instance said about it.
    /// </param>
    private static Line PollOptionLine(PostPoll poll, PostPollOption option, int width, string mark)
    {
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
        new Span(
            $"{BoostMark(post.Marks.Boosted)} {Number.Of(post.Boosts)}{Word(" boosts", spelledOut)}",
            post.Marks.Boosted ? Role.BoostMine : Role.Boost),
        new Span("   ", Role.Muted),
        new Span(
            $"{FavoriteMark(post.Marks.Favorited)} {Number.Of(post.Favorites)}{Word(" favorites", spelledOut)}",
            post.Marks.Favorited ? Role.FavoriteMine : Role.Favorite),
        new Span("   ", Role.Muted),
        new Span($"↩ {Number.Of(post.Replies)}{Word(" replies", spelledOut)}", Role.Muted),
        new Span(post.Marks.Pinned ? "   pinned" : string.Empty, Role.Muted),
    ]);

    private static string Word(string word, bool spelledOut) => spelledOut ? word : string.Empty;
}
