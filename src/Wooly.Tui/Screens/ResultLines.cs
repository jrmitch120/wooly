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
    /// <summary>How far a hashtag's counts stand off the end of the name column, so the two read as two columns.</summary>
    private const int TagGap = 2;

    /// <summary>The rows of <paramref name="result" />, at <paramref name="room" /> columns.</summary>
    /// <param name="result">What is drawn.</param>
    /// <param name="at">Where it stands in the walk, which says how far a post on it is read.</param>
    /// <param name="room">How wide it is drawn, behind the gutter.</param>
    /// <param name="drawing">What the screen is drawn with.</param>
    /// <param name="columns">The columns the run of hashtags it stands in is aligned to.</param>
    /// <param name="on">The screen it is drawn on, which knows how far each of its posts is read.</param>
    public static IReadOnlyList<Line> Of(
        Result result,
        int at,
        int room,
        Drawing drawing,
        Columns columns,
        Screen on) => result switch
    {
        // Nothing implied: a page of search results claims nothing of anybody on it, so the whole compact standing
        // is worth drawing — and nothing at all where the instance was never asked (#204).
        Result.OfAccount(var account) => AccountLines.Block(
            account,
            drawing.In(room),
            AccountLines.Compact(account.Standing)),
        Result.OfHashtag(var hashtag) => [Tag(hashtag, room, columns)],
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
    /// </remarks>
    public static IReadOnlyList<Line> Between(Result result) => result switch
    {
        Result.OfAccount => [Line.Blank],
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
    ///     use standing in the columns <paramref name="columns" /> gives, so the run reads down as well as across.
    /// </summary>
    /// <remarks>
    ///     The names being all different lengths, counts written straight after each one land wherever the name
    ///     happened to end, and comparing two tags becomes reading two rows rather than glancing down one column.
    ///     The name takes the column and the numbers are right-aligned inside theirs, which is where a digit belongs:
    ///     <c>81</c> under <c>14</c> compares at a glance and <c>81</c> under <c>1</c> does not.
    ///     <para>
    ///         Where the run has no columns — an empty one, or one the terminal is too narrow to hold — the gap falls
    ///         back to <see cref="TagGap" /> and the counts are written unpadded, which is the row this screen drew
    ///         before it had columns at all.
    ///     </para>
    /// </remarks>
    private static Line Tag(Hashtag hashtag, int width, Columns columns)
    {
        var name = TextWrap.Clip($"#{hashtag.Name}", width);

        // Never less than the gap itself, so a name longer than the column is still parted from its counts rather
        // than run into them.
        var gap = new string(' ', Math.Max(TagGap, columns.Name - Glyphs.Columns(name) + TagGap));

        return Line.Of([
            new Span(name, Role.BylineHandle),
            new Span(
                TextWrap.Clip(gap + Used(hashtag, columns), Math.Max(0, width - Glyphs.Columns(name))),
                Role.Muted),
        ]);
    }

    /// <summary>
    ///     How much use a tag has had lately, its two counts padded into the columns <paramref name="columns" />
    ///     gives — which is no padding at all where the run has no columns, <see cref="string.PadLeft(int)" /> of
    ///     nought being the number as it was written.
    /// </summary>
    /// <remarks>
    ///     Written in one place because it is both measured and drawn: a row padded to a width worked out from
    ///     anything but the row itself is a row that can be padded to the wrong one.
    /// </remarks>
    private static string Used(Hashtag hashtag, Columns columns) =>
        $"{Number.Of(hashtag.RecentPosts).PadLeft(columns.Posts)} posts · {Number.Of(hashtag.RecentAccounts).PadLeft(columns.Accounts)} accounts";

    /// <summary>
    ///     How wide the three columns of a run of hashtags are: the longest name, and the longest of each of the two
    ///     counts as they are written out.
    /// </summary>
    /// <remarks>
    ///     A fact about the run rather than about a tag, which is why it is worked out once and handed to every row:
    ///     a row that measured itself could only align with itself. Measured off what is drawn — <c>#</c> included,
    ///     and the counts grouped as <see cref="Number" /> groups them — rather than off the raw numbers, because
    ///     <c>1,204</c> takes a column more than <c>1204</c> does.
    /// </remarks>
    /// <param name="Name">How wide the name column is, <c>#</c> and all.</param>
    /// <param name="Posts">How wide the posts count is.</param>
    /// <param name="Accounts">How wide the accounts count is.</param>
    public readonly record struct Columns(int Name, int Posts, int Accounts)
    {
        /// <summary>
        ///     The columns a run of <paramref name="hashtags" /> wants at <paramref name="width" />, and none at all
        ///     where it is empty or where the terminal cannot hold them.
        /// </summary>
        /// <remarks>
        ///     Aligning is all or nothing, and it is the run that decides rather than the row: a row that gave up its
        ///     columns while its neighbours kept theirs would be the ragged run this exists to prevent. Where they do
        ///     not fit they are given up altogether rather than narrowed, because the padding is spent from the left
        ///     and the row is clipped from the right — so columns held past the width cost the accounts count that
        ///     would otherwise have fitted, which is a worse row than a ragged one.
        /// </remarks>
        public static Columns Of(IReadOnlyList<Hashtag> hashtags, int width)
        {
            if (hashtags.Count == 0)
            {
                return default;
            }

            var columns = new Columns(
                hashtags.Max(hashtag => Glyphs.Columns(hashtag.Name) + 1),
                hashtags.Max(hashtag => Glyphs.Columns(Number.Of(hashtag.RecentPosts))),
                hashtags.Max(hashtag => Glyphs.Columns(Number.Of(hashtag.RecentAccounts))));

            // Padded, every row's counts come out the same width, so the widest row is the longest name against any
            // row's counts and one of them is enough to measure.
            return columns.Name + TagGap + Glyphs.Columns(Used(hashtags[0], columns)) <= width ? columns : default;
        }
    }
}
