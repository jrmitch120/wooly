using Wooly.Core.Posts;
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
    ///     What marks a picture: the whole of what a reader has until its pixels arrive, and the whole of what they
    ///     have if none ever do — which on a terminal drawing coloured cells is nearly a picture anyway.
    /// </summary>
    private const string MediaMark = "▒▒▒▒";

    /// <summary>What stands in front of an attachment that is linked rather than drawn.</summary>
    private const string LinkMark = "⏵";

    /// <summary>One post as a feed shows it: a byline, the text, what is attached, and the three counts.</summary>
    /// <param name="post">The post, which may be a boost of another one.</param>
    /// <param name="width">How many columns there are, which at an 80-column terminal is 61.</param>
    /// <param name="revealed">Whether the reader has asked to see past a content warning.</param>
    /// <param name="now">What to measure the timestamp against.</param>
    public static IReadOnlyList<Line> Feed(Post post, int width, bool revealed, DateTimeOffset now)
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
        lines.AddRange(Media(shown, width, Inset.FeedRows));
        lines.Add(Counts(shown, spelledOut: false));

        return lines;
    }

    /// <summary>
    ///     The post whole, as the screen you drilled into: the same content, with the byline broken across two rows
    ///     and the moment said exactly rather than as an age.
    /// </summary>
    public static IReadOnlyList<Line> Whole(Post post, int width, bool revealed, DateTimeOffset now)
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
        lines.AddRange(Media(shown, width, Inset.WholeRows));
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
    ///     What is attached: the pictures in a band drawn in place, then a line each saying what they show, then the
    ///     attachments a terminal cannot draw — each of those as a link and its description, never as an inline
    ///     rendering attempt (story 51, ADR-0016).
    /// </summary>
    /// <param name="rows">How tall the band of pictures is, which is the one thing a feed and a whole post differ on.</param>
    private static IEnumerable<Line> Media(Post post, int width, int rows)
    {
        var drawn = post.Media.Where(attached => attached.IsDrawable).ToList();

        foreach (var line in Band(drawn, width, rows))
        {
            yield return line;
        }

        foreach (var attached in drawn)
        {
            yield return Described(attached, MediaMark, width);
        }

        foreach (var attached in post.Media.Where(attached => !attached.IsDrawable))
        {
            yield return Described(attached, LinkMark, width);

            // The address on rows of its own, wrapped rather than clipped: at 61 columns a real attachment address is
            // longer than the row, and a link with its end cut off is a link nobody can follow. Indented under the
            // mark, so a reader can see where it starts and where it stops.
            foreach (var row in TextWrap.Wrap(attached.Url, Math.Max(1, width - 2)))
            {
                yield return Line.Of($"  {row}", Role.Muted);
            }
        }
    }

    /// <summary>
    ///     The rows a post's pictures are drawn in. The box is kept whether or not the pixels are here yet, so a feed
    ///     does not jump under a reader as images land; the mark and the description below say what is in it until
    ///     they do (ADR-0016).
    /// </summary>
    private static IEnumerable<Line> Band(IReadOnlyList<PostMedia> pictures, int width, int rows)
    {
        var insets = Inset.Across(pictures, width, rows);

        if (insets.Count == 0)
        {
            yield break;
        }

        // The band's first row carries the boxes; the rest are rows of the screen that the boxes cover.
        yield return new Line([new Span(new string(' ', Inset.Width(insets)), Role.Media)]) { Insets = insets };

        for (var row = 1; row < rows; row++)
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
