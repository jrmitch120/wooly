using Wooly.Core.Posts;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Prototype.Separator;

internal delegate IReadOnlyList<Line> RowsOf(
    IReadOnlyList<Post> posts,
    int picked,
    int width,
    DateTimeOffset now,
    IPictures pictures);

/// <summary>One candidate answer to issue #62, and what it costs.</summary>
internal sealed record Variant(string Name, string Cost, RowsOf Rows);

/// <summary>
///     The five candidates from the ticket, plus today's baseline for comparison. Every variant is built from the real
///     <see cref="Picked{T}" />/<see cref="PostLines" /> pipeline — none of this reimplements how a post draws, only how
///     the space between two of them is spent.
/// </summary>
internal static class Variants
{
    public static readonly IReadOnlyList<Variant> All =
    [
        new("Today", "1 row per post — Line.Blank, and nothing else changes", Today),
        new("A — a rule where the blank was", "1 row per post — same cost as today, ink instead of whitespace", Rule),
        new(
            "B — every post carries a gutter mark, not only the picked one",
            "0 extra rows — the gutter column is already always spent (Picked<T>.Gutter)",
            EveryGutter),
        new(
            "C — the byline gets a rule of its own",
            "2 rows per post — the blank stays, plus a rule under the byline",
            HeavyByline),
        new(
            "D — the body indents under a flush byline",
            "0 extra rows — 2 columns off body/media/counts (approximated: text wrapped at full width, then shifted, so a long row may run to 63)",
            Indented),
        new("E — a blank above, a rule below", "2 rows per post — the most expensive candidate", BlankThenRule),
        new(
            "F — A, with an avatar and a two-row byline",
            "1 row per post for the rule (same as A), +2 rows for the byline split in two, "
            + "+5 columns for the avatar — spent only on those two rows, not on body/media/counts",
            AvatarThenRule),
    ];

    private static Picked<Post> PickedOf(IReadOnlyList<Post> posts, int at)
    {
        var picked = new Picked<Post>(posts);
        picked.Pick(at);

        return picked;
    }

    private static Draws<Post> DrawWith(DateTimeOffset now, IPictures pictures) =>
        (post, _, room) => PostLines.Feed(post, room, revealed: false, now, pictures);

    private static Line RuleLine(int width) => Line.Of(new string('─', width), Role.Muted);

    public static IReadOnlyList<Line> Today(
        IReadOnlyList<Post> posts, int picked, int width, DateTimeOffset now, IPictures pictures) =>
        PickedOf(posts, picked).Rows(width, DrawWith(now, pictures));

    public static IReadOnlyList<Line> Rule(
        IReadOnlyList<Post> posts, int picked, int width, DateTimeOffset now, IPictures pictures)
    {
        var pk = PickedOf(posts, picked);
        var draw = DrawWith(now, pictures);
        var lines = new List<Line>();

        for (var at = 0; at < pk.Count; at++)
        {
            lines.AddRange(pk.RowsOf(at, width, draw));
            lines.Add(RuleLine(width));
        }

        return lines;
    }

    public static IReadOnlyList<Line> EveryGutter(
        IReadOnlyList<Post> posts, int picked, int width, DateTimeOffset now, IPictures pictures)
    {
        var draw = DrawWith(now, pictures);
        var lines = new List<Line>();

        for (var at = 0; at < posts.Count; at++)
        {
            var isPicked = at == picked;
            var gutter = new Span(isPicked ? "▌" : "│", isPicked ? Role.Selection : Role.Muted);

            foreach (var line in draw(posts[at], at, Math.Max(1, width - 1)))
            {
                lines.Add(line.After(gutter).PartOf(at));
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    public static IReadOnlyList<Line> HeavyByline(
        IReadOnlyList<Post> posts, int picked, int width, DateTimeOffset now, IPictures pictures)
    {
        var pk = PickedOf(posts, picked);
        var draw = DrawWith(now, pictures);
        var lines = new List<Line>();

        for (var at = 0; at < pk.Count; at++)
        {
            var rows = pk.RowsOf(at, width, draw);

            // rows[0] is the byline, except for a boost — which none of the fakes are (approximation).
            lines.Add(rows[0]);
            lines.Add(RuleLine(width));
            lines.AddRange(rows.Skip(1));
            lines.Add(Line.Blank);
        }

        return lines;
    }

    public static IReadOnlyList<Line> Indented(
        IReadOnlyList<Post> posts, int picked, int width, DateTimeOffset now, IPictures pictures)
    {
        var draw = DrawWith(now, pictures);
        var lines = new List<Line>();

        for (var at = 0; at < posts.Count; at++)
        {
            var isPicked = at == picked;
            var gutter = new Span(isPicked ? "▌" : " ", isPicked ? Role.Selection : Role.Body);
            var rows = draw(posts[at], at, Math.Max(1, width - 1));

            for (var row = 0; row < rows.Count; row++)
            {
                var line = (row == 0 ? rows[row] : Indent(rows[row])).After(gutter).PartOf(at);
                lines.Add(line);
            }

            lines.Add(Line.Blank);
        }

        return lines;
    }

    public static IReadOnlyList<Line> BlankThenRule(
        IReadOnlyList<Post> posts, int picked, int width, DateTimeOffset now, IPictures pictures)
    {
        var pk = PickedOf(posts, picked);
        var draw = DrawWith(now, pictures);
        var lines = new List<Line>();

        for (var at = 0; at < pk.Count; at++)
        {
            lines.AddRange(pk.RowsOf(at, width, draw));
            lines.Add(Line.Blank);
            lines.Add(RuleLine(width));
        }

        return lines;
    }

    /// <summary>How wide the fake avatar block is — 4 columns of picture plus the gap after it, as in the reference.</summary>
    private const int AvatarCols = 4;

    public static IReadOnlyList<Line> AvatarThenRule(
        IReadOnlyList<Post> posts, int picked, int width, DateTimeOffset now, IPictures pictures)
    {
        var pk = PickedOf(posts, picked);
        Draws<Post> draw = (post, _, room) =>
        [
            .. AvatarHeader(post, room, now),
            .. RestOfFeed(post, room, now, pictures),
        ];
        var lines = new List<Line>();

        for (var at = 0; at < pk.Count; at++)
        {
            lines.AddRange(pk.RowsOf(at, width, draw));
            lines.Add(RuleLine(width));
        }

        return lines;
    }

    /// <summary>
    ///     The rest of what <see cref="PostLines.Feed" /> draws once its own one-row byline is dropped — body and
    ///     media unchanged, with a blank ahead of counts so the three marks read as a footer rather than one more line
    ///     of the body. An approximation: a boosted post's own note stays part of the byline being replaced here,
    ///     which none of the fakes exercise.
    /// </summary>
    private static IReadOnlyList<Line> RestOfFeed(Post post, int room, DateTimeOffset now, IPictures pictures)
    {
        var rest = PostLines.Feed(post, room, revealed: false, now, pictures).Skip(post.Boosted is null ? 1 : 2).ToList();

        return [.. rest[..^1], Line.Blank, rest[^1]];
    }

    /// <summary>
    ///     A fake avatar — pixels a real terminal would paint, same as the media band — beside a byline split across
    ///     two rows the way <c>tut</c> draws it: the name and the audience/age on one, the handle on the other. Spent
    ///     only here; body, media and counts revert to the full row.
    /// </summary>
    private static IReadOnlyList<Line> AvatarHeader(Post post, int room, DateTimeOffset now)
    {
        var avatar = new Span(new string('▓', AvatarCols), Role.Media);
        var gap = new Span(" ", Role.Body);
        var headerRoom = Math.Max(0, room - AvatarCols - 1);

        var tail = $"{PostLines.Audience(post.Visibility)} {Elapsed.Since(post.PostedAt, now)}";
        var nameRoom = Math.Max(0, headerRoom - tail.Length - 1);
        var name = TextWrap.Clip(post.Author, nameRoom);
        var pad = new string(' ', Math.Max(1, headerRoom - name.Length - tail.Length));

        var handle = TextWrap.Clip($"@{post.Account}", headerRoom);

        return
        [
            Line.Of([avatar, gap, new Span(name, Role.BylineName), new Span(pad, Role.Body), new Span(tail, Role.Audience)]),
            Line.Of([avatar, gap, new Span(handle, Role.BylineHandle)]),
            Line.Blank,
        ];
    }

    private static Line Indent(Line line) => new([new Span("  ", Role.Body), .. line.Spans])
    {
        Insets = line.Insets.Count == 0 ? line.Insets : [.. line.Insets.Select(inset => inset.ShiftedBy(2))],
        Wants = line.Wants,
        Item = line.Item,
    };
}
