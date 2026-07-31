using System.Globalization;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     A post as rows of spans: once for a feed, where it is one item among many, and once whole, where it is the
///     screen. Both name roles and neither knows what a colour is (ADR-0014).
///     <para>
///         Every state here has a glyph before it has a colour — <c>○ ◌ ● ✉</c> for the four audiences, <c>⚠</c> for a
///         warning, <c>↺</c> and <c>★</c> for the two marks, <c>▒▒▒▒</c> for a picture. That is not decoration: on a
///         terminal reporting no colour, and to a reader who cannot tell this green from that grey, the glyphs are the
///         whole of what is being said.
///     </para>
/// </summary>
public static class PostLines
{
    /// <summary>What stands in for a picture nothing in this ticket can draw. Media itself is #31.</summary>
    private const string MediaMark = "▒▒▒▒";

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
        lines.AddRange(Media(shown, width));
        lines.Add(Counts(shown));

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
                new Span($"{Audience(shown.Visibility)} {AudienceName(shown.Visibility)}", Role.Audience),
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
        lines.AddRange(Media(shown, width));
        lines.Add(Line.Blank);
        lines.Add(CountsSpelledOut(shown));

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

    /// <summary>Who can see a post, in the word a reader would use for it.</summary>
    public static string AudienceName(PostVisibility visibility) => PostVisibilityName.Of(visibility);

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
    ///     What is attached, as a mark and whatever its author said it shows. Drawing the thing itself is #31; saying
    ///     it is there, and saying what it is, is what this ticket owes a reader who cannot see it either way.
    /// </summary>
    private static IEnumerable<Line> Media(Post post, int width) =>
        post.Media.Select(attached => Line.Of([
            new Span($"{MediaMark} ", Role.Media),
            new Span(
                TextWrap.Clip(attached.Description ?? $"{Kind(attached.Kind)}, undescribed", width - MediaMark.Length - 1),
                attached.Description is null ? Role.Muted : Role.Media),
        ]));

    private static string Kind(MediaKind kind) => kind switch
    {
        MediaKind.Image => "a picture",
        MediaKind.Animation => "an animation",
        MediaKind.Video => "a video",
        MediaKind.Audio => "some audio",
        _ => "an attachment",
    };

    /// <summary>
    ///     The three counts. Each takes the role that says whether this profile is one of the accounts in it, which is
    ///     the whole reason a post carries the reader's own marks.
    /// </summary>
    private static Line Counts(Post post) => Line.Of([
        new Span($"↺ {Number(post.Boosts)}", post.Marks.Boosted ? Role.BoostMine : Role.Boost),
        new Span("   ", Role.Muted),
        new Span($"★ {Number(post.Favorites)}", post.Marks.Favorited ? Role.FavoriteMine : Role.Favorite),
        new Span("   ", Role.Muted),
        new Span($"↩ {Number(post.Replies)}", Role.Muted),
        new Span(post.Marks.Pinned ? "   pinned" : string.Empty, Role.Muted),
    ]);

    private static Line CountsSpelledOut(Post post) => Line.Of([
        new Span($"↺ {Number(post.Boosts)} boosts", post.Marks.Boosted ? Role.BoostMine : Role.Boost),
        new Span("   ", Role.Muted),
        new Span($"★ {Number(post.Favorites)} favorites", post.Marks.Favorited ? Role.FavoriteMine : Role.Favorite),
        new Span("   ", Role.Muted),
        new Span($"↩ {Number(post.Replies)} replies", Role.Muted),
    ]);

    private static string Number(long count) => count.ToString("N0", CultureInfo.CurrentCulture);
}
