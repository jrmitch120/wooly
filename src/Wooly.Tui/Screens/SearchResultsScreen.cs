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
///         No <see cref="Screen.Subject" />, the prompt's being its trends rather than the search: a live search is its
///         own question rather than a subject <c>g</c> could ask again, and asking it again is <c>esc</c> and <c>⏎</c>
///         (#84). What it holds is what the prompt's question found, handed to it whole.
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
            .. Result.Headed(
                new Result.Section(asked, SearchKind.Accounts),
                (results.Accounts ?? []).Select(Result (account) => new Result.OfAccount(account))),
            .. Result.Headed(
                new Result.Section(asked, SearchKind.Hashtags),
                (results.Hashtags ?? []).Select(Result (hashtag) => new Result.OfHashtag(hashtag))),
            .. Result.Headed(
                new Result.Section(asked, SearchKind.Posts),
                (results.Posts ?? []).Select(Result (post) => new Result.OfPost(post))),
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
    public SearchKind? PickedSection => _results.Out is Result.Section(_, var kind) ? kind : null;

    /// <inheritdoc />
    /// <remarks>
    ///     The post picked out, so that reading, answering and marking one a search found mean what they mean on a
    ///     feed. An account, a hashtag or a heading is not a post, and picking one leaves this empty rather than
    ///     guessing.
    /// </remarks>
    public override Post? Picked => _results.Out is Result.OfPost(var post) ? post : null;

    /// <inheritdoc />
    protected override IPicked Walking => _results;

    /// <summary>
    ///     What this screen's own <c>⏎</c> does: opens what is picked out — an account, a hashtag's timeline, or a post —
    ///     or, for a heading, that kind alone, asked again and read on (#604).
    /// </summary>
    public override Task Answer(Verb verb, Reach reach) => Result.Answer(verb, reach, _results.Out, Opens);

    /// <inheritdoc />
    public override bool Heard(Change change)
    {
        Result.Hear(_results, change);

        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     A click on a heading opens it, as <c>⏎</c> on it would — pushing that kind alone (#604) — since a heading
    ///     is picked out only to be opened, and a pick that needed a second gesture to do the one thing it can do would
    ///     be a pick in the way.
    /// </remarks>
    public override Verb Clicked(int? item, int? part, bool chorded) =>
        item is { } at && at < _results.Count && _results.All[at].Heads
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

            if (result is Result.Section(_, var kind))
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
    ///     The heading over the run of <paramref name="kind" />, counted against the <see cref="PerKind" /> asked
    ///     (<see cref="ResultLines.Heading" />). Part of the walk, so that it can be picked out and opened, which pushes
    ///     the kind alone.
    /// </summary>
    /// <remarks>
    ///     The count costs nothing, being in hand already, and it tells a reader whether the run below is worth walking,
    ///     worth jumping past, or worth opening for more (#166).
    /// </remarks>
    private Line Heading(SearchKind kind, int room) =>
        ResultLines.Heading(
            ResultLines.Called(kind),
            _results.All.Count(one => one is not Result.Section && ResultLines.KindOf(one) == kind),
            PerKind,
            room);
}
