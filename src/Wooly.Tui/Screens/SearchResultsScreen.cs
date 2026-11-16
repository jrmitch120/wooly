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
///         A preview of each kind: <see cref="PerKind" /> of each are asked for, and the heading over a run is walked to
///         and opened like a result, pushing that kind alone where more of it is read (<see cref="SectionScreen" />,
///         #604).
///     </para>
///     <para>
///         No <see cref="Screen.Subject" />, as the prompt has none: a live search is its own question rather than a
///         subject <c>g</c> could ask again, and asking it again is <c>esc</c> and <c>⏎</c> (#84). What it holds is what
///         the prompt's question found, handed to it whole.
///     </para>
/// </remarks>
public sealed class SearchResultsScreen : Screen
{
    /// <summary>
    ///     How many of each kind the mixed results ask for (#604): a preview, the screen of one kind being where more is
    ///     read. Asked outright rather than left to the instance's default, so that a heading can say the page came back
    ///     full against what was asked rather than against a number the instance might change.
    /// </summary>
    public const int PerKind = 20;

    /// <summary>
    ///     What the search found, all three kinds as the one list the reader walks, each run under its heading. Held
    ///     this way so that the order the results are drawn in and the order they are picked out in are the same order
    ///     by construction, rather than two counts kept in step by hand.
    /// </summary>
    private readonly Picked<Result> _results;

    /// <summary>What <paramref name="asked" /> found, accounts then hashtags then posts, each under its heading.</summary>
    /// <remarks>
    ///     Accounts, then hashtags, then posts — the order <see cref="SearchResults" /> holds them in, and from here
    ///     the only order there is. A kind nothing was found of gets no heading, rather than a heading over nothing.
    ///     The first result is picked out, as it was before headings could be: a heading is there to be walked up to,
    ///     not to stand between the reader and what was found.
    /// </remarks>
    /// <param name="asked">What was searched for, as it was typed.</param>
    /// <param name="results">What the instance answered.</param>
    public SearchResultsScreen(string asked, SearchResults results)
    {
        Asked = asked;

        _results = new Picked<Result>([
            .. Run(SearchKind.Accounts, (results.Accounts ?? []).Select(Result (account) => new Result.OfAccount(account))),
            .. Run(SearchKind.Hashtags, (results.Hashtags ?? []).Select(Result (hashtag) => new Result.OfHashtag(hashtag))),
            .. Run(SearchKind.Posts, (results.Posts ?? []).Select(Result (post) => new Result.OfPost(post))),
        ]);

        if (_results.Count > 1)
        {
            _results.Pick(1);
        }
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
    ///     prompt. <c>esc</c> back to the query is left unsaid, as it is on that timeline. A heading picked out opens
    ///     with <c>⏎</c>, as a result does.
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
    ///     already behave. Counted as the headings this screen draws rather than as the kinds a search has, which are
    ///     the same number here and would not be on a screen whose runs are not kinds: what the key can reach is what
    ///     stands under a heading, so that is what is counted — a search that turned up nothing but accounts draws one
    ///     heading, and one run has nowhere to jump to.
    /// </remarks>
    private IReadOnlyList<KeyHint> Jumping => _results.All.Count(result => result is Result.Section) > 1
        ? [new KeyHint("[/]", "section")]
        : [];

    /// <summary>What was searched for, as it was typed.</summary>
    public string Asked { get; }

    /// <summary>Which row of the walk is picked out, counted across the headings and all three kinds as one list.</summary>
    public int At => _results.At;

    /// <summary>How many results there are, of all three kinds together — the headings over them not among them.</summary>
    public int Count => _results.All.Count(result => result is not Result.Section);

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

    /// <summary>
    ///     The kind whose heading is picked out, or <see langword="null" /> where the pick is on a result rather than on
    ///     a heading.
    /// </summary>
    public SearchKind? PickedSection => _results.Out is Result.Section(var kind) ? kind : null;

    /// <inheritdoc />
    /// <remarks>
    ///     The post picked out, so that reading, answering and marking one a search found mean what they mean on a
    ///     feed. An account, a hashtag or a heading is not a post, and picking one leaves this empty rather than
    ///     guessing.
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
    ///     What a result opens as, wherever it was found — here or on the screen of its kind alone: an account, a
    ///     hashtag's timeline, or a post's thread — or nothing for a heading, which only the screen it heads can open. A
    ///     <b>Trending link</b>, which only the trends hold, opens as the posts sharing it (#607), on the screen of
    ///     trending links alone as under the prompt (#606).
    /// </summary>
    /// <remarks>
    ///     A hashtag opens as a screen on the stack rather than as a place on the rail. Which tags the rail keeps
    ///     places for are the tag sets the reader wrote down (<c>docs/tui-shell.md</c>), and a search result is not
    ///     them changing their mind about them.
    /// </remarks>
    internal static Subject? Opening(Result result) => result switch
    {
        Result.OfAccount(var account) => Opening(account),
        Result.OfHashtag(var hashtag) => new Subject.Tag(hashtag.Name),
        Result.OfPost(var post) => new Subject.Thread(post),
        Result.OfLink(var link) => new Subject.Sharing(link),
        _ => null,
    };

    /// <summary>
    ///     Opens whatever the reader picked out: an account, a hashtag's timeline, or a post — or, for a heading, that
    ///     kind alone, asked again and read on (#604).
    /// </summary>
    private Task OpenResult(Reach reach)
    {
        if (PickedSection is { } kind)
        {
            return reach.Open(new Subject.Narrowed(Asked, kind));
        }

        // A post opens through Opens rather than straight off the result, as on every screen holding posts.
        if (Picked is not null)
        {
            return Opens is { } post ? reach.Open(new Subject.Thread(post)) : Task.CompletedTask;
        }

        return _results.Out is { } result && Opening(result) is { } opening ? reach.Open(opening) : Task.CompletedTask;
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
    /// <remarks>
    ///     A click on a heading opens it, as <c>⏎</c> on it would — pushing that kind alone (#604) — since a heading
    ///     is picked out only to be opened, and a pick that needed a second gesture to do the one thing it can do would
    ///     be a pick in the way.
    /// </remarks>
    public override Verb Clicked(int? item, int? part, bool chorded) =>
        item is { } at && at < _results.Count && _results.All[at] is Result.Section
            ? Verb.OpenResult
            : base.Clicked(item, part, chorded);

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;

        if (Count == 0)
        {
            return [Line.Of(TextWrap.Clip($"Nothing found for {Asked}.", width), Role.Muted)];
        }

        var lines = new List<Line>();

        for (var at = 0; at < _results.Count; at++)
        {
            var result = _results.All[at];

            if (result is Result.Section(var kind))
            {
                // A blank above it wherever something stands above it, which is what separates one kind from the
                // next — the same blank AccountLines.Header puts over each of its sections, and no rule. The first
                // heading is the screen's first row and takes none.
                if (at > 0)
                {
                    lines.Add(Line.Blank);
                }

                lines.AddRange(_results.RowsOf(at, width, (_, _, room) => [Heading(kind, room)]));
                lines.Add(Line.Blank);

                continue;
            }

            if (_results.All[at - 1] is not Result.Section)
            {
                lines.AddRange(ResultLines.Between(result));
            }

            lines.AddRange(_results.RowsOf(at, width, (found, where, room) =>
                ResultLines.Of(found, where, room, drawing, this)));
        }

        return lines;
    }

    /// <summary>
    ///     <paramref name="found" /> under the heading over <paramref name="kind" />, or nothing at all where nothing of
    ///     it was found — a run with nothing in it draws no heading, and so is no section.
    /// </summary>
    private static IEnumerable<Result> Run(SearchKind kind, IEnumerable<Result> found)
    {
        var run = found.ToList();

        return run.Count == 0 ? [] : [new Result.Section(kind), .. run];
    }

    /// <summary>
    ///     The heading over the run of <paramref name="kind" />: what it is called, with how many were found — the
    ///     <c>── {n} replies ──</c> vocabulary the post screen already speaks — or <c>{n}+</c> where the instance served
    ///     as many as it was asked for, which is it saying there may be more than it served (#604).
    /// </summary>
    /// <remarks>
    ///     Marked as a heading rather than only drawn like one, which is what <c>[</c> and <c>]</c> move between and
    ///     what a jump brings onto the page with the result it lands on: the runs the reader can see are then the runs
    ///     the key knows about, with no second count of them kept anywhere (<see cref="Sections" />). And part of the
    ///     walk, so that it can be picked out and opened, which pushes the kind alone.
    ///     <para>
    ///         The count costs nothing, being in hand already, and it tells a reader whether the run below is worth
    ///         walking, worth jumping past, or worth opening for more (#166). Counted against what was asked for
    ///         rather than equal to it: an instance puts an exact-match hashtag ahead of the twenty it ranked (#599).
    ///     </para>
    /// </remarks>
    private Line Heading(SearchKind kind, int room)
    {
        var found = _results.All.Count(one => one is not Result.Section && ResultLines.KindOf(one) == kind);
        var more = found >= PerKind ? "+" : "";

        return Line.Of(TextWrap.Clip($"── {found}{more} {ResultLines.Called(kind)} ──", room), Role.Muted).Heading();
    }
}
