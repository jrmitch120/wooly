using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     What a search found, pushed over the prompt it was asked from as a screen of its own and crumbed with what was
///     asked — <c>Search › cats</c> — so that <c>esc</c> walks back to the prompt with the query still typed, for
///     refining rather than retyping (#600, <c>docs/tui-shell.md</c>).
/// </summary>
/// <remarks>
///     One search, three kinds of result, listed in the order <see cref="SearchResults" /> holds them — which is what
///     makes a search for a word somebody half-remembers worth typing at all, since they rarely know in advance which
///     of the three it will turn out to be (ADR-0011).
///     <para>
///         No <see cref="Screen.Subject" />, as the prompt has none: a live search is its own question rather than a
///         subject <c>g</c> could ask again, and asking it again is <c>esc</c> and <c>⏎</c> (#84). What it holds is what
///         the prompt's question found, handed to it whole.
///     </para>
/// </remarks>
public sealed class SearchResultsScreen : Screen
{
    /// <summary>How far a hashtag's counts stand off the end of the name column, so the two read as two columns.</summary>
    private const int TagGap = 2;

    /// <summary>
    ///     What the search found, all three kinds as the one list the reader walks. Held this way so that the order
    ///     the results are drawn in and the order they are picked out in are the same order by construction, rather
    ///     than two counts kept in step by hand.
    /// </summary>
    private readonly Picked<Result> _results;

    /// <summary>What <paramref name="asked" /> found, accounts then hashtags then posts.</summary>
    /// <remarks>
    ///     Accounts, then hashtags, then posts — the order <see cref="SearchResults" /> holds them in, and from here
    ///     the only order there is.
    /// </remarks>
    /// <param name="asked">What was searched for, as it was typed.</param>
    /// <param name="results">What the instance answered.</param>
    public SearchResultsScreen(string asked, SearchResults results)
    {
        Asked = asked;

        _results = new Picked<Result>([
            .. (results.Accounts ?? []).Select(Result (account) => new Result.OfAccount(account)),
            .. (results.Hashtags ?? []).Select(Result (hashtag) => new Result.OfHashtag(hashtag)),
            .. (results.Posts ?? []).Select(Result (post) => new Result.OfPost(post)),
        ]);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     What was asked, and nothing more: the crumb under it is <c>Search</c>, the prompt, so the breadcrumb reads
    ///     <c>Search › cats</c> — the query said once, where the stack is said.
    /// </remarks>
    public override string Crumb => Asked;

    /// <inheritdoc />
    /// <remarks>
    ///     The keys the results offered while they shared a screen with the prompt, unchanged by being pushed over it
    ///     (#600): <c>tab</c> along the rail as on a hashtag's timeline walked to from here, and <c>/</c> for a fresh
    ///     prompt. <c>esc</c> back to the query is left unsaid, as it is on that timeline.
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
        Picked is not null
            ? PostKeys.Around(
                new KeyHint("j/k", "result"),
                [.. Jumping, new KeyHint("/", "search again")],
                PostKeys.AlongTheRail)
            :
            [
                new KeyHint("j/k", "result"),
                .. Jumping,
                new KeyHint("⏎", "open", NeedsAPick: true),
                new KeyHint("/", "search again"),
                .. PostKeys.Leaving(PostKeys.AlongTheRail),
            ];

    /// <summary>
    ///     The key that moves between this screen's sections, said only where two or more kinds found something — and
    ///     <c>section</c> rather than <c>kind</c>, because one key that means one thing shell-wide is named one way
    ///     shell-wide (#166, amended by #172).
    /// </summary>
    /// <remarks>
    ///     A key announced where it does nothing reads as a shell that missed the press, which is how the poll digits
    ///     already behave. Counted as the headed runs this screen draws rather than as the kinds a search has, which
    ///     are the same number here and would not be on a screen whose runs are not kinds: what the key can reach is
    ///     what stands under a heading (<see cref="Heads" />), so that is what is counted — a search that turned up
    ///     nothing but accounts draws one heading, and one run has nowhere to jump to.
    /// </remarks>
    private IReadOnlyList<KeyHint> Jumping => Enumerable.Range(0, _results.Count).Count(Heads) > 1
        ? [new KeyHint("[/]", "section")]
        : [];

    /// <summary>What was searched for, as it was typed.</summary>
    public string Asked { get; }

    /// <summary>Which result is picked out, counted across all three kinds as one list.</summary>
    public int At => _results.At;

    /// <summary>How many results there are, of all three kinds together.</summary>
    public int Count => _results.Count;

    /// <summary>The accounts that were found.</summary>
    public IReadOnlyList<Account> Accounts => [.. _results.All.OfType<Result.OfAccount>().Select(one => one.Account)];

    /// <summary>The hashtags that were found.</summary>
    public IReadOnlyList<Hashtag> Hashtags => [.. _results.All.OfType<Result.OfHashtag>().Select(one => one.Hashtag)];

    /// <summary>The posts that were found.</summary>
    public IReadOnlyList<Post> Posts => [.. _results.All.OfType<Result.OfPost>().Select(one => one.Post)];

    /// <inheritdoc />
    public override IEnumerable<Mentionable> Seen => [.. Accounts.Select(Mentionable.Of), .. Mentionable.In(Posts)];

    /// <summary>The account picked out, or <see langword="null" /> where the picked result is not one.</summary>
    public Account? PickedAccount => _results.Out is Result.OfAccount(var account) ? account : null;

    /// <summary>The hashtag picked out, or <see langword="null" /> where the picked result is not one.</summary>
    public Hashtag? PickedHashtag => _results.Out is Result.OfHashtag(var hashtag) ? hashtag : null;

    /// <inheritdoc />
    /// <remarks>
    ///     The post picked out, so that reading, answering and marking one a search found mean what they mean on a
    ///     feed. An account or a hashtag is not a post, and picking one leaves this empty rather than guessing.
    /// </remarks>
    public override Post? Picked => _results.Out is Result.OfPost(var post) ? post : null;

    /// <inheritdoc />
    protected override IPicked Walking => _results;

    /// <summary>What this screen's own <c>⏎</c> does: opens what is picked out.</summary>
    public override Task Answer(Verb verb, Reach reach) =>
        verb == Verb.OpenResult ? OpenResult(reach) : Task.CompletedTask;

    /// <summary>What an account a search found opens as, whether it was picked out here or named at the prompt.</summary>
    internal static Subject Opening(Account account) =>
        new Subject.Account(AccountAddress.Parse(account.Address), WithReplies: false);

    /// <summary>
    ///     Opens whatever the search turned up and the reader picked out: an account, a hashtag's timeline, or a post.
    /// </summary>
    /// <remarks>
    ///     A hashtag opens as a screen on the stack rather than as a place on the rail. Which tags the rail keeps
    ///     places for are the tag sets the reader wrote down (<c>docs/tui-shell.md</c>), and a search result is not
    ///     them changing their mind about them.
    /// </remarks>
    private Task OpenResult(Reach reach)
    {
        if (PickedAccount is { } account)
        {
            return reach.Open(Opening(account));
        }

        if (PickedHashtag is { } hashtag)
        {
            return reach.Open(new Subject.Tag(hashtag.Name));
        }

        return Opens is { } post ? reach.Open(new Subject.Thread(post)) : Task.CompletedTask;
    }


    /// <inheritdoc />
    public override bool Heard(Change change)
    {
        switch (change)
        {
            case Change.PostChanged(var post):
                _results.Rewrite(found =>
                    found is Result.OfPost(var held) ? new Result.OfPost(PostChange.Replaced(held, post)) : found);

                break;

            case Change.PostGone(var id):
                _results.Remove(found => found is Result.OfPost(var held) && PostChange.Names(held, id));

                break;
        }

        return false;
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;

        if (Count == 0)
        {
            return [Line.Of(TextWrap.Clip($"Nothing found for {Asked}.", width), Role.Muted)];
        }

        var lines = new List<Line>();

        // Worked out once for the whole run rather than per row, a column being a fact about the run and not about
        // any one tag in it — and measured against the room a row actually gets, which is behind the gutter.
        var columns = Columns.Of(Hashtags, Stamped.Room(width));

        for (var at = 0; at < _results.Count; at++)
        {
            lines.AddRange(Heads(at) ? Heading(at) : Between(_results.All[at]));
            lines.AddRange(_results.RowsOf(at, width, Draw));
        }

        return lines;

        IReadOnlyList<Line> Draw(Result result, int at, int room) => result switch
        {
            // Nothing implied: a page of search results claims nothing of anybody on it, so the whole compact
            // standing is worth drawing — and nothing at all where the instance was never asked (#204).
            Result.OfAccount(var account) => AccountLines.Block(
                account,
                drawing.In(room),
                AccountLines.Compact(account.Standing)),
            Result.OfHashtag(var hashtag) => [Tag(hashtag, room, columns)],
            Result.OfPost(var post) => PostLines.Feed(post, drawing.In(room), ReadingOf(post, at)),
            _ => [],
        };
    }

    /// <summary>
    ///     What a kind of result is called, which is what the heading over the run of them says — with how many were
    ///     found, the <c>── {n} replies ──</c> vocabulary the post screen already speaks.
    /// </summary>
    /// <remarks>
    ///     The count costs nothing, being in hand already, and it tells a reader whether the run below is worth
    ///     walking or worth jumping past (#166).
    /// </remarks>
    private static string Called(Result result, int found) => result switch
    {
        Result.OfAccount => $"── {found} accounts ──",
        Result.OfHashtag => $"── {found} hashtags ──",
        _ => $"── {found} posts ──",
    };

    /// <summary>
    ///     The heading over the result at <paramref name="at" />, which is the first of its kind — so a kind nothing
    ///     was found of gets no heading, rather than a heading over nothing.
    /// </summary>
    /// <remarks>
    ///     Marked as a heading rather than only drawn like one, which is what <c>[</c> and <c>]</c> move between and
    ///     what a jump brings onto the page with the result it lands on: the runs the reader can see are then the runs
    ///     the key knows about, with no second count of them kept anywhere (<see cref="Sections" />).
    ///     <para>
    ///         A blank above it wherever something stands above it, which is what separates one kind from the next —
    ///         the same blank <see cref="AccountLines.Header" /> puts over each of its sections, and no rule. The
    ///         first heading is the screen's first row and takes none.
    ///     </para>
    /// </remarks>
    private IReadOnlyList<Line> Heading(int at)
    {
        var result = _results.All[at];

        // The whole run rather than what is left of it, which is the same number here — the results of a kind are
        // contiguous, being the order the instance's three lists were laid end to end in.
        var found = _results.All.Count(one => one.GetType() == result.GetType());

        Line[] heading = [Line.Of(Called(result, found), Role.Muted).Heading(), Line.Blank];

        return at == 0 ? heading : [Line.Blank, .. heading];
    }

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
    private static IReadOnlyList<Line> Between(Result result) => result switch
    {
        Result.OfAccount => [Line.Blank],
        Result.OfHashtag => [],
        _ => [Line.Rule()],
    };

    /// <summary>
    ///     Whether the result at <paramref name="at" /> is the first of its kind, and so the one a heading stands
    ///     over — which is the whole of where a run begins on this screen.
    /// </summary>
    /// <remarks>
    ///     Asked of the kind rather than of what a heading says, so that two kinds could never come to share one
    ///     heading by being called the same thing. Said here rather than twice, because the runs
    ///     <see cref="Jumping" /> counts have to be the runs <see cref="Heading" /> draws: a status row announcing a
    ///     key against a second count of its own is how the two come to disagree — on this screen the day a kind stops
    ///     earning a heading, and on the next screen to take this key the day its runs are not kinds at all.
    /// </remarks>
    private bool Heads(int at) => at == 0 || _results.All[at - 1].GetType() != _results.All[at].GetType();

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
    private readonly record struct Columns(int Name, int Posts, int Accounts)
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
