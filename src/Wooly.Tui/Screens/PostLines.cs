using Wooly.Core;
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
    ///     What stands in front of a content warning, wherever one is on screen — over a warned post's text, over the
    ///     same text once it is shown, where a sensitive post's attachments are being kept, and on the row a reply's
    ///     own warning is written in (#123). Said here because <see cref="Warning" /> is where a warning is drawn, and
    ///     shared rather than typed a second time so that a warning being written looks like the warning it becomes.
    /// </summary>
    internal const string WarningMark = "⚠ ";

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
    ///     How many columns an attachment's box gives up on each side, walked or not: the columns a walked picture's
    ///     tall brackets are drawn in, given up by every attachment's box so that walking to one moves nothing and a
    ///     video's preview lines up with a picture's beside it (ADR-0034, #519). A link preview's picture is never
    ///     walked to as a picture, and keeps the whole width.
    /// </summary>
    private const int Bracketed = 1;

    /// <summary>
    ///     One post as a feed shows it: what it boosts and what it answers, a two-row byline with the author's avatar
    ///     beside it, the text, what is attached, and the three counts behind a blank of their own.
    /// </summary>
    /// <param name="post">The post, which may be a boost of another one.</param>
    /// <param name="drawing">
    ///     The room, the moment, what this terminal can paint and what the reader asked for — everything about
    ///     drawing this post that is not about the post (#148).
    /// </param>
    /// <param name="reading">
    ///     What this reader has done to this post — asked past its warning, walked to a reference in it — which is
    ///     <see langword="default" /> for the posts nobody has touched (#95).
    /// </param>
    /// <param name="saysHowToAskPast">
    ///     Whether the <c>x  show it</c> row is drawn under whatever the post is hiding. On for every screen that picks
    ///     the post out, and off for exactly one that does not: the conversations list, where a row is a conversation
    ///     and <c>x</c> has no post to be asked about — so the row would be naming a key that cannot act, which reads
    ///     as a shell that missed the press (<c>docs/tui-shell.md</c>, #120). The warning itself is drawn either way.
    ///     <para>
    ///         An argument of its own rather than a field on <see cref="Drawing" />, along with
    ///         <see cref="Whole" />'s <c>saysWhatItAnswers</c>: a drawing is one value the shell makes per frame and
    ///         every screen hands down unchanged but for the room, while these two are facts about <em>this</em>
    ///         drawing of <em>this</em> post and differ between two rows of the same frame. Put on the record they
    ///         would be adjustable everywhere it goes and would stay adjusted, which is a row further down quietly
    ///         drawn under a rule nobody set for it.
    ///     </para>
    /// </param>
    public static IReadOnlyList<Line> Feed(
        Post post,
        Drawing drawing,
        Reading reading,
        bool saysHowToAskPast = true)
    {
        var width = drawing.Width;
        var show = OnShow.Of(post, reading);
        var shown = show.Shown;

        return Parts(
            [
                .. Boosted(post, $"{post.Author} boosted", drawing),
                .. Answering(shown, width),
                .. Byline(shown, drawing),
            ],
            [
                Body(show, drawing, reading, saysHowToAskPast),
                .. Media(show, drawing, Inset.FeedRows, reading, saysHowToAskPast),
                LinkPreview(show, drawing, Inset.FeedRows, reading),
                Poll(show, drawing, reading),
            ],
            [Counts(shown, spelledOut: false)]);
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
        Drawing drawing,
        Reading reading,
        bool saysWhatItAnswers = true)
    {
        var width = drawing.Width;
        var show = OnShow.Of(post, reading);
        var shown = show.Shown;
        var avatar = Avatar.Byline(shown.Account, shown.AvatarUrl, drawing.Pictures, drawing.Raster);
        var room = Math.Max(0, width - avatar.Width);

        return Parts(
            [
                .. Boosted(post, $"boosted by {post.Author}", drawing),
                .. saysWhatItAnswers ? Answering(shown, width) : [],

                // Three byline rows rather than the feed's two, the third being the moment said exactly rather than as
                // an age — and stepped in with the other two though the avatar's box has run out below it, because two
                // rows starting in one column and a third starting in another would read as two things.
                .. avatar.Across(
                    CustomEmojiBox.Clipped(shown.Author, shown.AuthorCustomEmoji, room, Role.BylineName, drawing),
                    Line.Of(TextWrap.Clip($"@{shown.Account}", room), Role.BylineHandle),
                    Line.Of([
                        new Span(Elapsed.Moment(shown.PostedAt), Role.Muted),
                        new Span(" · ", Role.Muted),
                        new Span(
                            $"{Audience(shown.Visibility)} {PostVisibilityName.Of(shown.Visibility)}",
                            Role.Audience),
                    ])),
            ],
            [
                // The post screen is about this post, so x is always something that can act on it here.
                Body(show, drawing, reading, saysHowToAskPast: true),
                .. Media(show, drawing, Inset.WholeRows, reading, saysHowToAskPast: true),
                LinkPreview(show, drawing, Inset.WholeRows, reading),
                Poll(show, drawing, reading),
            ],
            [Counts(shown, spelledOut: true)]);
    }

    /// <summary>
    ///     The most rows <see cref="Preview" /> draws: enough to say which post it was, and short enough not to be read
    ///     again in place of the notification it sits under (#452).
    /// </summary>
    private const int PreviewRows = 2;

    /// <summary>
    ///     A reminder of which post this is rather than the post: its first two rows of text, muted, with nothing
    ///     attached and no link preview — and where it has a content warning, the warning in place of the text, so a
    ///     preview never says what the warning was put up to keep back (#452).
    /// </summary>
    /// <remarks>
    ///     What a notification about the reader's own post draws under it. They wrote it and already know it, so what
    ///     the row is for is who did what to it; <c>⏎</c> still opens it whole.
    ///     <para>
    ///         Its first row is marked as where the post's content starts (<see cref="Line.ContentStarts" />), as
    ///         <see cref="Feed" />'s is.
    ///     </para>
    /// </remarks>
    public static IReadOnlyList<Line> Preview(Post post, Drawing drawing) =>
        [.. Previewed(post, drawing).Select((line, at) => at == 0 ? line.StartingContent() : line)];

    /// <summary>The rows <see cref="Preview" /> draws, with nothing marked on them.</summary>
    /// <remarks>
    ///     Its custom emoji are drawn as the post's own words draw them, and held whole by the cut as by the wrap — a
    ///     warning's in place of the words alike (ADR-0035).
    /// </remarks>
    private static IReadOnlyList<Line> Previewed(Post post, Drawing drawing)
    {
        var width = drawing.Width;
        var shown = post.Boosted ?? post;

        if (shown.ContentWarning is { } warning)
        {
            return [CustomEmojiBox.Clipped($"{WarningMark}{warning}", shown.CustomEmoji, width, Role.Muted, drawing)];
        }

        // The custom emoji written in text, found as the post's own words find them.
        IReadOnlyList<Shortcode> Written(string text) =>
            BodyText.CustomEmoji(text, BodyText.References(text), shown.CustomEmoji);

        // A row of the preview, muted, its custom emoji set as their boxes.
        Line Muted(string text, IReadOnlyList<Shortcode> written, int at = 0) =>
            CustomEmojiBox.Boxed(CustomEmojiBox.Written(text, Role.Muted, written, at), drawing);

        var shortcodes = Written(shown.Content);

        var rows = TextWrap.Rows(shown.Content, width, CustomEmojiBox.Held(shortcodes, drawing))
                           .Where(row => !string.IsNullOrWhiteSpace(row.Text))
                           .ToList();

        if (rows.Count <= PreviewRows)
        {
            return [.. rows.Select(row => Muted(row.Text, shortcodes, row.At))];
        }

        // The last row carries the rest of the text and is cut with the ellipsis Clip marks a cut with, so the
        // preview says it stops short rather than reading as the whole post. The rest is joined into a text of its
        // own, so its shortcodes are found afresh in it.
        var rest = string.Join(' ', rows.Skip(PreviewRows - 1).Select(row => row.Text));
        var clipped = TextWrap.Clip(rest, width, CustomEmojiBox.Held(Written(rest), drawing));

        return
        [
            .. rows.Take(PreviewRows - 1).Select(row => Muted(row.Text, shortcodes, row.At)),
            Muted(clipped, Written(clipped)),
        ];
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
    ///     <para>
    ///         The first row of the first part of <paramref name="content" /> with anything in it is marked as where
    ///         the post's content starts (<see cref="Line.ContentStarts" />): the rows above it say who wrote the post,
    ///         and the counts under it say what was done to it, so neither is what a reader scrolling past a byline is
    ///         reading.
    ///     </para>
    /// </remarks>
    private static IReadOnlyList<Line> Parts(
        IEnumerable<Line> heading,
        IEnumerable<IEnumerable<Line>> content,
        IEnumerable<Line> foot)
    {
        var lines = new List<Line>();
        var started = false;

        Add(heading, starts: false);

        foreach (var part in content)
        {
            started |= Add(part, starts: !started);
        }

        Add(foot, starts: false);

        return lines;

        bool Add(IEnumerable<Line> part, bool starts)
        {
            var rows = part.ToList();

            if (rows.Count == 0)
            {
                return false;
            }

            if (lines.Count > 0)
            {
                lines.Add(Line.Blank);
            }

            lines.Add(starts ? rows[0].StartingContent() : rows[0]);
            lines.AddRange(rows.Skip(1));

            return true;
        }
    }

    /// <summary>
    ///     Who boosted this, where somebody did — <paramref name="said" /> because a feed names the booster first and
    ///     a post screen puts them after the fact, and the role turns on whether the booster was the reader.
    /// </summary>
    /// <remarks>
    ///     The booster's custom emoji drawn as their byline draws them (#524). Found in the whole of
    ///     <paramref name="said" />, whose own words have no colon in them to open one, and clipped with it, so that
    ///     the line is cut where it always was and an emoji at the cut is dropped whole.
    /// </remarks>
    private static IEnumerable<Line> Boosted(Post post, string said, Drawing drawing)
    {
        if (post.Boosted is not null)
        {
            yield return CustomEmojiBox.Clipped(said, post.AuthorCustomEmoji, drawing.Width - 2, Role.Muted, drawing)
                                       .After(new Span(
                                           $"{BoostMark(post.Marks.Boosted)} ",
                                           post.Marks.Boosted ? Role.BoostMine : Role.Boost));
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
        PostVisibility.Followers => "●",
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
    private static IEnumerable<Line> Byline(Post post, Drawing drawing)
    {
        var avatar = Avatar.Byline(post.Account, post.AvatarUrl, drawing.Pictures, drawing.Raster);
        var room = Math.Max(0, drawing.Width - avatar.Width);

        var tail = new Span($"{Audience(post.Visibility)} {Elapsed.Since(post.PostedAt, drawing.Now)}", Role.Audience);

        // Measured as drawn, its custom emoji two columns each where the Raster draws, so the tail is pushed to the
        // same column whatever the name holds (#524).
        var name = CustomEmojiBox.Clipped(
            post.Author,
            post.AuthorCustomEmoji,
            Math.Max(0, room - tail.Width - 1),
            Role.BylineName,
            drawing);

        return
        [
            .. avatar.Across(
                name.Then(
                    new Span(new string(' ', Math.Max(1, room - name.Width - tail.Width)), Role.Body),
                    tail),
                Line.Of(TextWrap.Clip($"@{post.Account}", room), Role.BylineHandle)),
        ];
    }

    /// <summary>
    ///     The text, or the warning standing in front of it. A warning is honoured rather than printed past: the
    ///     author put the post behind it, and a client that showed both would have made the warning pointless.
    /// </summary>
    /// <param name="show">
    ///     What this post is showing this reader (#145). <see cref="OnShow.Words" /> is what settles this half: the
    ///     text stands behind the content warning alone, the instance's flag being a mark over media.
    /// </param>
    /// <param name="reading">What the reader has done to this post: asked past its warning, picked a reference in it.</param>
    /// <param name="saysHowToAskPast">
    ///     Whether the row naming <c>x</c> goes under the warning. The text stays behind it either way — what is off on
    ///     the one screen that says no is the offer, not the hiding (#120).
    /// </param>
    private static IEnumerable<Line> Body(OnShow show, Drawing drawing, Reading reading, bool saysHowToAskPast)
    {
        var post = show.Shown;
        var lines = new List<Line>();

        // The warning is read for what it says rather than for whether to hide anything, which is show.Words'. A post
        // showing its words still draws it: the reader asked past it, and taking it down would leave them reading
        // something they were warned about with the warning gone.
        if (post.ContentWarning is { } warning)
        {
            if (!show.Words)
            {
                return Hiding(warning, post.CustomEmoji, drawing, saysHowToAskPast);
            }

            lines.Add(Warning(warning, post.CustomEmoji, drawing));
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
        // The custom emoji the same way, found once and held whole by the wrap where they are drawn, so the rows are
        // laid out for the room the Raster gives them from the first frame rather than for whether the pictures have
        // landed (ADR-0035). A warned post's are never wanted until its words are shown, as the rest of it is.
        var references = BodyText.References(post.Content);
        var shortcodes = BodyText.CustomEmoji(post.Content, references, post.CustomEmoji);

        lines.AddRange(BodyText.Lines(post.Content, references, reading.Reference, shortcodes, drawing));

        return lines;
    }

    /// <summary>
    ///     One warning, said the one way: the mark and what is being warned about. Written here rather than at each of
    ///     the three places one goes up — over a warned post's text, over the same text once it is shown, and where a
    ///     sensitive post's attachments are being kept — so a warning cannot come to look like two different things on
    ///     one post.
    /// </summary>
    /// <remarks>
    ///     Its custom emoji are drawn as the post's words draw them, off the post's own list, which the instance keeps
    ///     for the warning and the words alike — and held whole by the clip, a warning being one row however long.
    ///     They are drawn whether the warning is hiding the words or not, being part of the warning rather than of what
    ///     it hides (ADR-0035, #526).
    /// </remarks>
    /// <param name="said">What is being warned about.</param>
    /// <param name="listed">The custom emoji the post lists, or none for a warning this client writes.</param>
    /// <param name="drawing">How the screen draws, and how wide the row is.</param>
    private static Line Warning(string said, IReadOnlyList<CustomEmoji> listed, Drawing drawing) =>
        CustomEmojiBox.Clipped(
                said,
                listed,
                drawing.Width - Glyphs.Columns(WarningMark),
                Role.ContentWarning,
                drawing)
            .After(new Span(WarningMark, Role.ContentWarning));

    /// <summary>
    ///     What stands in place of what a post is hiding: the warning, and under it the row naming the key that would
    ///     show it — where there is a key that can act. Both of the things a post hides behind go up this way, so that
    ///     what asks and whether it is asked at all cannot come to differ between them (#120).
    /// </summary>
    private static Line[] Hiding(
        string said,
        IReadOnlyList<CustomEmoji> listed,
        Drawing drawing,
        bool saysHowToAskPast) => saysHowToAskPast
        ? [Warning(said, listed, drawing), Line.Of(AskPastIt, Role.Muted)]
        : [Warning(said, listed, drawing)];

    /// <summary>
    ///     What is attached: a picture drawn in place or the address to reach an undrawn one at, and for everything
    ///     else — a video, an animation, a sound, or an attachment of a kind this client has no word for — the label
    ///     <c>←</c>/<c>→</c> walks to and <c>⏎</c> opens, with a video's or an animation's own preview drawn in a box
    ///     under it where the instance offered one (ADR-0017).
    /// </summary>
    /// <remarks>
    ///     Which case an attachment falls into is settled here rather than at the view, because it changes how many
    ///     rows the post takes. A picture this terminal can draw gets a box, reserved from the shape the instance
    ///     reported whether or not its pixels have landed, and its Stand-in until they do (ADR-0025); a picture this
    ///     terminal cannot draw at all gets the link and description the CLI already gives it (ADR-0016). There is no
    ///     cell-by-cell fallback: a photograph reduced to one coloured block per cell is not a picture of anything
    ///     (ADR-0016).
    ///     <para>
    ///         The split is <see cref="PostMedia.Opens" /> rather than <see cref="PostMedia.IsDrawable" />, and the two
    ///         stopped being opposites in #110: a video is walked <em>and</em> drawn. What tells the two halves apart
    ///         is what the row above the box says — a picture's description, which the box stands in for, against a
    ///         video's label, which says what opening it would reach and is not something a still frame can stand in
    ///         for at all.
    ///     </para>
    /// </remarks>
    /// <param name="show">
    ///     What this post is showing this reader (#145). <see cref="OnShow.Media" /> is what settles this half, and it
    ///     answers to either part of the warning: the instance's flag hides what is attached with no text to read it
    ///     off (#113).
    /// </param>
    /// <param name="drawing">
    ///     The width there is, what is here, how this terminal paints pixels, what a Stand-in's blur comes from, and
    ///     <see cref="Drawing.HideDrawnCaption" />: whether what an attachment says it shows drops where a box is drawn
    ///     for it (#71) — a picture's own caption, and since #110 a video's description under its label, on the same
    ///     terms. Since ADR-0025 that is wherever the box is reserved, from the first frame and not from when the
    ///     pixels land: the box is there either way, and a caption taken down on arrival would move the post under the
    ///     reader.
    /// </param>
    /// <param name="mostRows">
    ///     The most rows a picture may take, which is what a feed and a whole post differ on.
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
        OnShow show,
        Drawing drawing,
        int mostRows,
        Reading reading,
        bool saysHowToAskPast)
    {
        var width = drawing.Width;
        var post = show.Shown;

        // A warned post's attachments are part of what the warning covers, and nothing about one is drawn until the
        // reader has asked past it: no box, no label, no description, no address — and no Wants, which is what keeps
        // the pixels from being sent for at all (#113, ADR-0016's amendment).
        if (!show.Media)
        {
            // Where the post's own warning is not already asking, this is: a photograph marked sensitive under no
            // warning at all would otherwise be hidden with nothing on screen to say a key meant anything. Inside this
            // branch, words still on show is exactly that case — nothing is hidden here but by the instance's flag,
            // and the flag alone leaves the text where it is. There is always something to say it about, a post
            // warned by the flag alone being one carrying attachments or a link preview (Post.IsWarned). It stands
            // above where both of them would be, and is said once rather than per thing, because it is one post hiding
            // things rather than a prompt for each (#113, #116).
            if (show.Words)
            {
                yield return Hiding(SensitiveMedia, [], drawing, saysHowToAskPast);
            }

            yield break;
        }

        var draws = Drawing.Draws(drawing.Pictures, drawing.Raster, out _);

        for (var at = 0; at < post.Media.Count; at++)
        {
            var attached = post.Media[at];

            // Off the same formula Screen.References walks, so a bracket here and the pick the reader made can never
            // come to disagree about which attachment it landed on (AttachmentReferences).
            var reference = AttachmentReferences.For(post, at);

            if (attached.Opens)
            {
                yield return Opened(attached, reference, reading.Reference, drawing, mostRows);

                continue;
            }

            if (!draws || !AttachmentReferences.IsPicture(attached))
            {
                yield return [.. LinkedImage(attached, width)];

                continue;
            }

            // The description first, so it does not move when the picture lands underneath it — and marked as wanting
            // a picture, which is how the view knows to send for one if this post is near enough to the screen.
            var drawn = Drawn.Attached(attached);
            var described = Described(attached, MediaMark, width) with { Wants = drawn };

            if (AttachmentBox(drawn, attached, drawing, mostRows) is not { } box)
            {
                yield return [described];

                continue;
            }

            // Hidden from the first frame where the reader asked for it, not from when the picture lands: the box is
            // there either way, so a caption taken down on arrival would be a row going and the post moving under the
            // reader, which is what the box was reserved to stop (ADR-0025). The box's own top row carries the Wants
            // then, there being no caption to carry it. Walked to as the picture it is, its box bracketed while it is
            // (ADR-0034).
            var beside = new BracketColumns(reference, Walked: reading.Reference == reference);

            yield return drawing.HideDrawnCaption
                ? [.. Reserved(box, drawn, beside)]
                : [described, .. Reserved(box, bracketed: beside)];
        }
    }

    /// <summary>
    ///     The box an attachment's picture or preview is drawn in: shaped as a link preview's is, but in the width less
    ///     the <see cref="Bracketed" /> column either side, which it gives up whether or not it is walked (ADR-0034).
    /// </summary>
    private static PictureBox? AttachmentBox(Drawn drawn, PostMedia attached, Drawing drawing, int mostRows) =>
        PictureBox.Shaped(
            drawn,
            attached.Shape,
            attached.Blurhash,
            drawing.In(Math.Max(0, drawing.Width - (2 * Bracketed))),
            mostRows);

    /// <summary>
    ///     A picture that is not being drawn: what it shows, and where to get it. Unaffected by ADR-0017 and ADR-0034 —
    ///     an <c>Image</c> joins the walk only where it is drawn, so this is exactly what the CLI already prints
    ///     (ADR-0016).
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
    ///     something. Only the description after it goes, and only behind the same preference a picture's caption
    ///     already hides behind (#71) — what a box under it has taken over saying, from the frame it is reserved in.
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
        Drawing drawing,
        int mostRows)
    {
        Line Label(bool saysWhatItShows) =>
            AttachmentReferenceLine(attached, reference, picked, drawing.Width, saysWhatItShows);

        if (!attached.IsDrawable || !Drawing.Draws(drawing.Pictures, drawing.Raster, out _))
        {
            return [Label(saysWhatItShows: true)];
        }

        // Marked as wanting the preview whether or not it is here yet, the way a picture's own description is: this
        // row is what the view reads to decide the pixels are worth sending for.
        var drawn = Drawn.Attached(attached);

        // The description goes behind the preference from the first frame rather than when the preview lands, for the
        // reason a picture's caption does: the box is already there, and the setting should not depend on how quick
        // the network was (ADR-0025).
        return AttachmentBox(drawn, attached, drawing, mostRows) is not { } box
            ? [Label(saysWhatItShows: true) with { Wants = drawn }]
            :
            [
                Label(saysWhatItShows: !drawing.HideDrawnCaption) with { Wants = drawn },
                .. Reserved(box, bracketed: new BracketColumns()),
            ];
    }

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
    ///     Whether the author's description is written after the label. Off where a preview's box is reserved under it
    ///     and the reader has asked for that (#71, ADR-0025); the label itself is never off, so nothing on the row
    ///     moves either way.
    /// </param>
    private static Line AttachmentReferenceLine(
        PostMedia attached,
        Reference reference,
        Reference? picked,
        int width,
        bool saysWhatItShows)
    {
        var spans = MarkAndLabel(Capitalized(MediaKindName.Written(attached.Kind)), reference, picked == reference);

        if (saysWhatItShows && attached.Description is { } description)
        {
            var used = spans.Sum(span => span.Width) + 1;

            spans.Add(new Span($" {TextWrap.Clip(description, Math.Max(0, width - used))}", Role.Media));
        }

        return new Line(spans);
    }

    /// <summary>
    ///     The front of a row <c>←</c>/<c>→</c> walks to: the mark and <paramref name="label" />, in the brackets a
    ///     picked reference is drawn in where <paramref name="bracketed" />, the label and brackets saying they are
    ///     <paramref name="reference" />'s (<see cref="Span.Reference" />). Handed back open, for a caller with more
    ///     to say on the row — an attachment's description follows it.
    /// </summary>
    /// <remarks>
    ///     Said once for an attachment's kind and a link preview's title, so the two cannot come to mark a pick
    ///     differently — the brackets are what a reader reads a pick off, on a terminal with no colour and to a reader
    ///     who cannot tell one colour from another (ADR-0014).
    /// </remarks>
    private static List<Span> MarkAndLabel(string label, Reference? reference, bool bracketed)
    {
        var spans = new List<Span> { new($"{LinkMark} ", Role.Media) };

        if (bracketed)
        {
            spans.Add(new Span(BodyText.Opening, Role.ReferencePicked) { Reference = reference });
        }

        spans.Add(new Span(label, Role.Media) { Reference = reference });

        if (bracketed)
        {
            spans.Add(new Span(BodyText.Closing, Role.ReferencePicked) { Reference = reference });
        }

        return spans;
    }

    /// <summary>One word, its first letter upper-cased — <c>MediaKindName.Written</c>'s own spelling, said out loud.</summary>
    private static string Capitalized(string word) => $"{char.ToUpperInvariant(word[0])}{word[1..]}";

    /// <summary>
    ///     The rows of a box reserved before its picture arrives: its <b>Stand-in</b> while the pixels are not here,
    ///     and the picture fitted and centred inside it once they are — the same number of rows either way, so the
    ///     picture landing, or being let go of, or never coming at all moves nothing under it (ADR-0025).
    /// </summary>
    /// <remarks>
    ///     The Stand-in is a plain shade the size of the box with no text in it. The caption is said once, on its own
    ///     row above the box, and a second copy inside the shade would go when the picture came and would show where
    ///     the reader turned captions off (ADR-0025). A lookup and nothing else: asking never sends for anything, and
    ///     only the view — the one thing that knows where the scroll has got to — may do that (ADR-0016).
    ///     <para>
    ///         Said once for an attachment's box, a video's preview and a link preview's picture (#348), so the three
    ///         cannot come to reserve, shade or fit differently.
    ///     </para>
    ///     <para>
    ///         What each row holds and what is set into it is the <see cref="PictureBox" />'s, shaped from what the
    ///         instance reported; this lays the box across the row, between its bracket columns where it has them, and
    ///         says what its top row waits for.
    ///     </para>
    /// </remarks>
    /// <param name="box">The box the shape settled.</param>
    /// <param name="wants">
    ///     What the box's top row says it is waiting for, where no caption above it is there to say so — or
    ///     <see langword="null" /> where one is.
    /// </param>
    /// <param name="bracketed">
    ///     The <see cref="Bracketed" /> column either side of an attachment's box, or <see langword="null" /> for a
    ///     link preview's, which has none and starts at the row's left.
    /// </param>
    private static IEnumerable<Line> Reserved(PictureBox box, Drawn? wants = null, BracketColumns? bracketed = null)
    {
        var column = bracketed is null ? 0 : Bracketed;
        var reference = bracketed?.Reference;

        // Blank columns carrying the picture's reference, so that a click on them walks to it.
        Span Referring(int columns) => PictureBox.Blanks(columns, Role.Body) with { Reference = reference };

        Span Beside(string glyph, bool walked) =>
            walked ? new Span(glyph, Role.ReferencePicked) { Reference = reference } : Referring(column);

        for (var row = 0; row < box.Rows; row++)
        {
            var held = box.Cells(row);
            var (opening, closing) = PictureBrackets(row, box.Rows);

            // Every row is held out to the box's right edge and on through the column past it, so that the closing
            // bracket has its column whatever the pixels left blank on this row — blanks the page would have shown
            // there anyway — and every cell of it carries the picture's reference, so that a click anywhere on the box
            // or beside it walks to the picture and a double click opens it (ADR-0034).
            var cells = bracketed is not { } columns
                ? Line.Of(held)
                : Line.Of(
                    Beside(opening, columns.Walked),
                    held with { Reference = reference },
                    Referring(box.Columns - held.Width),
                    Beside(closing, columns.Walked));
            var line = cells with { Insets = box.Insets(row, column) };

            yield return row == 0 ? line with { Wants = wants } : line;
        }
    }

    /// <summary>
    ///     The <see cref="Bracketed" /> column either side of an attachment's box, which every attachment's box gives up
    ///     whether or not it is walked (ADR-0034).
    /// </summary>
    /// <param name="Reference">
    ///     The still picture's reference where the box is one, which every cell of every row carries, the box's own and
    ///     its bracket columns alike — so that the walk enters at it, lets it go and brings it on to the page by
    ///     whichever of its rows are there, as it does a wrapped link's (<see cref="Span.Reference" />), so that a
    ///     picture is walked only where its box is drawn (<see cref="Screen.LaidOut" />), and so that a click anywhere
    ///     on it walks to it — or <see langword="null" /> for a box nothing walks to, a video's preview.
    /// </param>
    /// <param name="Walked">
    ///     Whether <paramref name="Reference" /> is the one walked to, which draws the brackets in the two columns
    ///     (<see cref="PictureBrackets" />) rather than leaving them blank.
    /// </param>
    private readonly record struct BracketColumns(Reference? Reference = null, bool Walked = false);

    /// <summary>
    ///     The brackets either side of row <paramref name="row" /> of a walked picture's <paramref name="rows" />-row
    ///     box: <c>⎡</c> and <c>⎤</c> on its top row, <c>⎢</c> and <c>⎥</c> down the middle and <c>⎣</c> and <c>⎦</c>
    ///     on its bottom row, or <c>[</c> and <c>]</c> on a box one row tall (ADR-0034).
    /// </summary>
    /// <remarks>
    ///     A glyph a row rather than one bracket drawn over the box, so that a box partly scrolled off still shows its
    ///     brackets on the rows that are on the page. In <see cref="Role.ReferencePicked" />, the role <c>‹ ›</c> are
    ///     drawn in, and read as brackets without it on a terminal with no colour.
    /// </remarks>
    private static (string Opening, string Closing) PictureBrackets(int row, int rows) =>
        rows == 1 ? ("[", "]")
        : row == 0 ? ("⎡", "⎤")
        : row == rows - 1 ? ("⎣", "⎦")
        : ("⎢", "⎥");

    /// <summary>One attachment behind <paramref name="mark" />, saying what its author said it shows.</summary>
    private static Line Described(PostMedia attached, string mark, int width) => Line.Of([
        new Span($"{mark} ", Role.Media),
        new Span(
            TextWrap.Clip(attached.Shows, width - Glyphs.Columns(mark) - 1),
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
    /// <param name="show">
    ///     What this post is showing this reader (#145) — <see cref="OnShow.Media" />, the same question its
    ///     attachments are asked, which is the whole of what "behind the warning exactly as an attachment is" means.
    /// </param>
    /// <param name="mostRows">The most rows the picture may take, which is what a feed and a whole post differ on.</param>
    private static IReadOnlyList<Line> LinkPreview(OnShow show, Drawing drawing, int mostRows, Reading reading)
    {
        var post = show.Shown;
        var width = drawing.Width;

        if (post.LinkPreview is not { } link || !show.Media)
        {
            return [];
        }

        // Null on a terminal that draws nothing and where the instance chose no picture alike — the two answers that
        // read the same, which is what "linked rather than drawn" already means for an attachment (ADR-0016).
        var drawn = Drawing.Draws(drawing.Pictures, drawing.Raster, out _) ? Drawn.LinkPreview(link) : null;

        // The words first and always, so nothing a reader is looking at moves when the pixels land underneath them.
        // The walked row is what carries the Wants, the way an attachment's own description does.
        List<Line> lines =
        [
            LinkPreviewLine(link, LinkPreviewReference.Of(post), reading.Reference, width) with { Wants = drawn },
            .. LinkPreviewSays(link, width),
        ];

        // Reserved from the shape the card gave, or the 16:9 default where it gave none worth trusting, whether or not
        // the picture is here yet — the same box and Stand-in an attachment gets, so a link card moves nothing when its
        // picture lands either (ADR-0025, #348). The walked row above already carries the Wants.
        if (drawn is not null && PictureBox.Shaped(drawn, link.Shape, link.Blurhash, drawing, mostRows) is { } box)
        {
            lines.AddRange(Reserved(box));
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
        var room = width
                   - Glyphs.Columns(LinkMark)
                   - 1
                   - (bracketed ? Glyphs.Columns(BodyText.Opening) + Glyphs.Columns(BodyText.Closing) : 0);

        return new Line(MarkAndLabel(TextWrap.Clip(link.Name, Math.Max(0, room)), reference, bracketed));
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
    ///         That is the one place this parts company with <see cref="Media" /> and <see cref="LinkPreview" />: it
    ///         asks <see cref="OnShow.Words" /> where they ask <see cref="OnShow.Media" />, which answers to either
    ///         half. A real distinction rather than a drift, which is why the two are separate answers there and this
    ///         one names the half it wants (#145).
    ///     </para>
    ///     <para>
    ///         Nothing is said in place of it. A post carrying a warning is already showing it and already naming
    ///         <c>x</c>, and a second prompt under that would be the same offer made twice — the reason
    ///         <see cref="Media" /> prints its own only where no warning is already asking (#113, #116).
    ///     </para>
    /// </remarks>
    private static IEnumerable<Line> Poll(OnShow show, Drawing drawing, Reading reading)
    {
        var width = drawing.Width;

        if (show.Shown.Poll is not { } poll || !show.Words)
        {
            yield break;
        }

        // Only where something is actually toggled: an empty ballot is a poll nobody is voting in yet, and drawing
        // boxes down it would say a vote is being cast whenever a post with a poll on it is picked out.
        var chosen = reading.Chosen is { Count: > 0 } toggled ? toggled : null;

        for (var at = 0; at < poll.Options.Count; at++)
        {
            var option = poll.Options[at];

            // The row is the poll's own and the CLI draws the same one; the ballot's boxes are the one thing only
            // this surface has to lead a row with, and are the only reason it says what leads a row at all (#150).
            var row = chosen is null
                ? PollBar.RowOf(poll, option)
                : PollBar.RowOf(poll, option, chosen.Contains(at) ? "[x] " : "[ ] ");

            // The option's text is the row's last part, so the custom emoji it is written with are found in it alone
            // and moved along by what comes before it: the bar, the share and the count keep the columns they take on
            // a row without one, and only the option is drawn as a post's words are (ADR-0035).
            var text = row.Length - option.Text.Length;

            yield return CustomEmojiBox.Clipped(
                row,
                [
                    .. Shortcode.In(option.Text, option.CustomEmoji)
                                .Select(shortcode => shortcode with { At = shortcode.At + text }),
                ],
                width,
                Role.Poll,
                drawing);
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

    /// <summary>How many votes the poll has drawn, and from how many accounts where that can differ from the count.</summary>
    private static string VoteCountText(PostPoll poll) => poll.MultipleChoice && poll.Voters is { } voters
        ? $"{Number.Of(poll.Votes)} votes from {Number.Of(voters)} accounts"
        : $"{Number.Of(poll.Votes)} votes";

    /// <summary>
    ///     The three counts. A boost or favorite count takes the role that says whether this profile is one of the
    ///     accounts in it, which is the whole reason a post carries the reader's own marks; the reply count has no
    ///     <em>mine</em>, and takes <see cref="Role.Replies" />.
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
        new Span($"↩ {Number.Of(post.Replies)}{Word(" replies", spelledOut)}", Role.Replies),
        new Span(post.Marks.Pinned ? "   pinned" : string.Empty, Role.Muted),
    ]);

    private static string Word(string word, bool spelledOut) => spelledOut ? word : string.Empty;
}
