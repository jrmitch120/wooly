using Wooly.Core;
using Wooly.Core.Search;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     How a <see cref="Result" /> is drawn, wherever one is: on the mixed results of a search, and on the screen of
///     one kind of them pushed from a heading there (#604) — one way, so that a hashtag read on to past the twentieth
///     looks like the twenty above it.
/// </summary>
internal static class ResultLines
{
    /// <summary>How far a hashtag's counts stand off its name, at the least, so the two never run together.</summary>
    private const int TagGap = 2;

    /// <summary>
    ///     How many columns each of a hashtag's two counts is right-aligned in: room for <c>999,999</c>, more than a
    ///     week's use of all but the busiest tags. A count wider than that takes what it needs from its own row's name,
    ///     and moves no other row.
    /// </summary>
    private const int CountColumns = 7;

    /// <summary>How wide a hashtag's counts are, padded into their columns: what the room must hold besides a name.</summary>
    private static readonly int CountsWidth = Glyphs.Columns(Used(
        new Hashtag { Name = string.Empty, RecentPosts = 0, RecentAccounts = 0 },
        CountColumns));

    /// <summary>The rows of <paramref name="result" />, at <paramref name="room" /> columns.</summary>
    /// <param name="result">What is drawn.</param>
    /// <param name="at">Where it stands in the walk, which says how far a post on it is read.</param>
    /// <param name="room">How wide it is drawn, behind the gutter.</param>
    /// <param name="drawing">What the screen is drawn with.</param>
    /// <param name="on">The screen it is drawn on, which knows how far each of its posts is read.</param>
    public static IReadOnlyList<Line> Of(
        Result result,
        int at,
        int room,
        Drawing drawing,
        Screen on) => result switch
    {
        // Nothing implied: a page of search results claims nothing of anybody on it, so the whole compact standing
        // is worth drawing — and nothing at all where the instance was never asked (#204).
        Result.OfAccount(var account) => AccountLines.Block(
            account,
            drawing.In(room),
            AccountLines.Compact(account.Standing)),
        Result.OfHashtag(var hashtag) => [Tag(hashtag, room)],
        Result.OfPost(var post) => PostLines.Feed(post, drawing.In(room), on.ReadingOf(post, at)),
        _ => [],
    };

    /// <summary>
    ///     What stands between two results of the same kind, which is a different thing for each of the three — and
    ///     between rather than after, so nothing is left hanging under the last result on the screen.
    /// </summary>
    /// <remarks>
    ///     Posts keep the rule a feed puts between them, because a run of them here is a feed and reads like one
    ///     everywhere else (#62). Accounts take the blank every other screen listing people takes, four-row Account
    ///     blocks laid end to end being what runs together (#180, #198). Hashtags take nothing at all: one row apiece
    ///     under a heading that has already said how many there are, where a separator per row would spend as many
    ///     rows on the gaps as on the tags and the gutter already says which one is picked out.
    ///     <para>
    ///         A <b>Trending link</b> takes a blank too: three rows apiece, links run together without one (#605).
    ///     </para>
    /// </remarks>
    public static IReadOnlyList<Line> Between(Result result) => result switch
    {
        Result.OfAccount or Result.OfLink => [Line.Blank],
        Result.OfHashtag => [],
        _ => [Line.Rule()],
    };

    /// <summary>
    ///     What a kind of result is called, plural and lower case — what a heading over a run of them says, and what
    ///     the screen of that kind alone is crumbed with, capitalised.
    /// </summary>
    public static string Called(SearchKind kind) => kind switch
    {
        SearchKind.Accounts => "accounts",
        SearchKind.Hashtags => "hashtags",
        _ => "posts",
    };

    /// <summary>Which kind <paramref name="result" /> is.</summary>
    public static SearchKind KindOf(Result result) => result switch
    {
        Result.OfAccount => SearchKind.Accounts,
        Result.OfHashtag => SearchKind.Hashtags,
        Result.Section(var kind) => kind,
        _ => SearchKind.Posts,
    };

    /// <summary>
    ///     A tag and how much use it has had lately, which is what makes one result worth reading over another — the
    ///     use standing against the right edge of <paramref name="room" /> in columns of a fixed width, so the run reads
    ///     down as well as across.
    /// </summary>
    /// <remarks>
    ///     The names being all different lengths, counts written straight after each one land wherever the name
    ///     happened to end, and comparing two tags becomes reading two rows rather than glancing down one column. The
    ///     counts stand against the right edge instead, each number right-aligned inside a column of
    ///     <see cref="CountColumns" />, which is where a digit belongs: <c>81</c> under <c>14</c> compares at a glance
    ///     and <c>81</c> under <c>1</c> does not.
    ///     <para>
    ///         Every column comes from the room a row has and from nothing in the run, so that nothing jumps: a longer
    ///         name or a bigger count read on to further down moves none of the rows already drawn (#598's review), as
    ///         the trending hashtags under the prompt stand their count against the right edge (<see cref="TrendLines" />).
    ///         Where the room cannot hold the counts' columns, every row gives them up — the room deciding, so it is
    ///         every row or none — and falls back to <see cref="TagGap" /> and unpadded counts: padding is spent from the
    ///         left and the row is clipped from the right, so columns held past the width would cost the accounts count
    ///         that fits without them.
    ///     </para>
    /// </remarks>
    private static Line Tag(Hashtag hashtag, int room)
    {
        var aligned = room >= CountsWidth + TagGap;
        var used = Used(hashtag, aligned ? CountColumns : 0);
        var name = TextWrap.Clip($"#{hashtag.Name}", aligned ? Math.Max(0, room - Glyphs.Columns(used) - TagGap) : room);

        // Never less than the gap itself, so a name is parted from its counts rather than run into them.
        var gap = aligned ? room - Glyphs.Columns(name) - Glyphs.Columns(used) : TagGap;

        return Line.Of([
            new Span(name, Role.BylineHandle),
            new Span(TextWrap.Clip(new string(' ', gap) + used, Math.Max(0, room - Glyphs.Columns(name))), Role.Muted),
        ]);
    }

    /// <summary>
    ///     How much use a tag has had lately, each count padded to <paramref name="columns" /> — which is no padding at
    ///     all for nought, <see cref="string.PadLeft(int)" /> of nought being the number as it was written.
    /// </summary>
    private static string Used(Hashtag hashtag, int columns) =>
        $"{Number.Of(hashtag.RecentPosts).PadLeft(columns)} posts · {Number.Of(hashtag.RecentAccounts).PadLeft(columns)} accounts";
}
