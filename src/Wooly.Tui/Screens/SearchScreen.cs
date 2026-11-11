using System.Drawing;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Core.Trends;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The prompt <c>/</c> opens, with the instance's <b>Trend</b>s under it. A rail destination like any other, which
///     is why <c>/</c> is a frame key: it means "go to search" everywhere, and this is what it arrives at
///     (<c>docs/tui-shell.md</c>).
/// </summary>
/// <remarks>
///     What a search finds is pushed over it as a screen of its own (<see cref="SearchResultsScreen" />), and a
///     <see cref="DirectQuery" /> opens what it names straight over it — so <c>esc</c> from either comes back here with
///     the query as it was typed, for refining rather than retyping (#600), and the trends as they were left, asking
///     nothing. Choosing Search on the rail, or <c>/</c>, arrives at a fresh one, whose trends are read afresh.
///     <para>
///         What has been typed is not held here: it is a real one-line field's, laid over the field's row as compose's
///         content warning is laid over its header (#320, ADR-0030), so that it has a caret that moves, a selection,
///         and copy, cut and paste (#608). The prompt says where the field goes (<see cref="FieldAt" />) and, given
///         what the field held when <c>⏎</c> was pressed in it, what asking for it does (<see cref="Find" />).
///     </para>
///     <para>
///         The field and the trends are one walk, the field at the top of it (#602, #605): picked, the field takes
///         letters; with a trend picked, they are keys again — <c>j</c> and <c>k</c> walk, and a post's keys act on a
///         trending post. The field is drawn as a box titled with the instance acted as, lit while it takes letters and
///         dimmed while it does not, and it marks its own focus: no selection bar or band beside it.
///     </para>
/// </remarks>
public sealed class SearchScreen : Screen
{
    /// <summary>How many trending hashtags are drawn under the prompt (#602).</summary>
    public const int TrendingHashtags = 10;

    /// <summary>How many trending links are drawn under the prompt (#602).</summary>
    public const int TrendingLinks = 10;

    /// <summary>
    ///     How many trending posts are drawn under the prompt: three whole posts, which is what keeps the hashtags and
    ///     links above them from being pushed off the page (#602).
    /// </summary>
    public const int TrendingPosts = 3;

    /// <summary>What an empty field says, dimly, of what it takes.</summary>
    public const string Placeholder = "A word, a #hashtag, an @account, or a link";

    /// <summary>
    ///     How far into the field's row the field begins, past the gutter, the box's edge and the glass ahead of it:
    ///     <c> │ ⌕ </c>.
    /// </summary>
    private const int FieldLeft = 5;

    /// <summary>
    ///     How many columns of the field's row the box takes besides the field: <see cref="FieldLeft" />, and a space and
    ///     the box's edge after it.
    /// </summary>
    private const int FieldFrame = FieldLeft + 2;

    /// <summary>
    ///     The field, then the trends — hashtags, links and posts, in that order — as the one list the reader walks, so
    ///     that the order they are drawn in and the order they are picked out in are the same order by construction.
    /// </summary>
    private Picked<Result> _walk = new([new Result.Field()]);

    /// <summary>
    ///     Whether the trends have been read — and so whether an empty walk under the field is nothing trending.
    /// </summary>
    private bool _read;

    /// <param name="instance">The instance acted as, which answers the search and titles the field.</param>
    public SearchScreen(string instance) => Instance = instance;

    /// <summary>The instance acted as: whose answer a search gets, and what the field's box is titled with.</summary>
    public string Instance { get; }

    /// <inheritdoc />
    public override string Crumb => "Search";

    /// <summary>Whether the field is picked out, and so takes letters — rather than a trend under it.</summary>
    public bool OnTheField => _walk.Out is Result.Field;

    /// <summary>
    ///     Whether the first trend is picked out — the one <c>↑</c> walks up from onto the field, as <c>↓</c> walks down
    ///     to it from the field.
    /// </summary>
    public bool OnTheFirstTrend => _walk.At == 1 && _walk.Count > 1;

    /// <summary>The trending hashtags under the prompt.</summary>
    public IReadOnlyList<Hashtag> Hashtags => [.. _walk.All.OfType<Result.OfHashtag>().Select(one => one.Hashtag)];

    /// <summary>The trending links under the prompt.</summary>
    public IReadOnlyList<TrendingLink> Links => [.. _walk.All.OfType<Result.OfLink>().Select(one => one.Link)];

    /// <summary>The trending posts under the prompt.</summary>
    public IReadOnlyList<Post> Posts => [.. _walk.All.OfType<Result.OfPost>().Select(one => one.Post)];

    /// <summary>The trending hashtag picked out, or <see langword="null" /> where the pick is on anything else.</summary>
    public Hashtag? PickedHashtag => _walk.Out is Result.OfHashtag(var hashtag) ? hashtag : null;

    /// <summary>The trending link picked out, or <see langword="null" /> where the pick is on anything else.</summary>
    public TrendingLink? PickedLink => _walk.Out is Result.OfLink(var link) ? link : null;

    /// <inheritdoc />
    /// <remarks>
    ///     A trending post picked out, so that reading, answering and marking one mean what they mean on a feed.
    /// </remarks>
    public override Post? Picked => _walk.Out is Result.OfPost(var post) ? post : null;

    /// <inheritdoc />
    public override IEnumerable<Mentionable> Seen => Mentionable.In(Posts);

    /// <inheritdoc />
    protected override IPicked Walking => _walk;

    /// <inheritdoc />
    /// <remarks>
    ///     The keys that do something on the row picked out, and nothing else. On the field, only the keys that are not
    ///     letters, every letter being typed — <c>/</c> and <c>?</c> among them, which a web address and a question are
    ///     both entitled to contain — and the two that are never typed: <c>↓</c> into the trends, and <c>[</c> and
    ///     <c>]</c> between their sections. On a trend, the keys of a list: a trending post's keys where one is picked,
    ///     <c>⏎</c> where what is picked opens, and <c>/</c> back up to the field.
    ///     <para>
    ///         No <c>esc</c>: search is a rail destination, so the prompt is the bottom of the stack and there is
    ///         nothing under it to walk back to. <c>tab</c> is how you leave, the same as on every other destination.
    ///     </para>
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys
    {
        get
        {
            var runs = _walk.All.Skip(1).Select(KindOf).Distinct().Count();

            if (OnTheField)
            {
                return
                [
                    new KeyHint("⏎", "search"),
                    .. _walk.Count > 1 ? [new KeyHint("↓", "trends")] : Array.Empty<KeyHint>(),
                    .. runs > 0 ? [new KeyHint("[/]", "section")] : Array.Empty<KeyHint>(),
                    PostKeys.ToADestination,
                ];
            }

            // From a trend, [ and ] reach another section only where there is one: the field is no section of theirs.
            KeyHint[] jumping = runs > 1 ? [new KeyHint("[/]", "section")] : [];
            var moving = new KeyHint("j/k", "trend");
            var searching = new KeyHint("/", "search");

            if (Picked is not null)
            {
                return PostKeys.Around(moving, [.. jumping, searching], PostKeys.AlongTheRail);
            }

            return
            [
                moving,
                .. jumping,
                .. PickedHashtag is not null ? [new KeyHint("⏎", "open")] : Array.Empty<KeyHint>(),
                searching,
                .. PostKeys.Leaving(PostKeys.AlongTheRail),
            ];
        }
    }

    /// <summary>
    ///     The trends read for this prompt, under its field in the order they are drawn — the pick left where it was,
    ///     which is on the field: the reader may already be typing.
    /// </summary>
    internal void Trending(IReadOnlyList<Hashtag> hashtags, IReadOnlyList<TrendingLink> links, IReadOnlyList<Post> posts)
    {
        var at = _walk.At;

        _walk = new Picked<Result>([
            new Result.Field(),
            .. hashtags.Select(Result (hashtag) => new Result.OfHashtag(hashtag)),
            .. links.Select(Result (link) => new Result.OfLink(link)),
            .. posts.Select(Result (post) => new Result.OfPost(post)),
        ]);

        _walk.Pick(at);
        _read = true;
    }

    /// <summary>
    ///     Back up to the field from a trend, with whatever it holds kept — what <c>/</c> does with a trend picked, so
    ///     that carrying on typing is one key from anywhere under the field (#605).
    /// </summary>
    public void ToTheField() => PickAndFollow(0);

    /// <summary>
    ///     Where the field goes on a prompt whose rows are drawn <paramref name="width" /> columns wide: inside the box's
    ///     middle row, after the glass — the third row down, under the blank row and the box's top edge.
    /// </summary>
    public Rectangle FieldAt(int width) =>
        new(FieldLeft, 2, Math.Max(0, Stamped.Room(width) + 1 - FieldFrame), 1);

    /// <summary>Asks the instance for <paramref name="query" />, what the field held when <c>⏎</c> was pressed in it.</summary>
    /// <remarks>
    ///     The search itself is one call, so a rate limit leaves nothing to draw and is waited out rather than
    ///     half-answered (ADR-0011) — which <see cref="Enquiry" /> already does, and is why this reads like every
    ///     other fetch. The standing the accounts run is then decorated with is the second call and is not that kind of
    ///     call: it answers with nothing where it was refused, so a rate limit there costs the suffix and leaves the
    ///     results standing (<see cref="Searched" />).
    ///     <para>
    ///         What was found goes up over this prompt, the one it was asked from, and only while it is still in front
    ///         — rather than over whichever screen is on top when it arrives (#232).
    ///     </para>
    /// </remarks>
    public Task Find(string query, Reach reach)
    {
        if (!SearchQuery.IsWellFormed(query))
        {
            // The same words the command turns an empty query down with, so that the two front ends cannot come to
            // say different things about the same empty value.
            reach.Say(SearchQuery.Rejection, isError: true);

            return Task.CompletedTask;
        }

        // Nothing to ask the instance where what was typed names a tag, since any well-formed tag has a timeline. It
        // is pushed exactly as picking it from the results would push it, and this screen is left as it was — the
        // prompt, its field still holding what was typed — for esc to come back to.
        if (DirectQuery.Tag(query) is { } tag)
        {
            return reach.Open(new Subject.Tag(tag));
        }

        // A handle or an address asks the one search it always asked, an address in full so that it is resolved
        // rather than searched for as words. Where it found exactly what was named, that is opened straight over the
        // prompt as picking it out would open it, with no results beneath it — so one esc comes back to what was typed
        // (#600). Anything else pushes its results, the near miss among them.
        var typed = query.Trim();
        var asking = SearchQuery.For(
            DirectQuery.Asking(typed),
            page: SearchPage.Of(limit: SearchResultsScreen.PerKind));

        return reach.Put(
            ask => Searched(ask, reach, asking),
            ifStillHere: found =>
            {
                if (DirectQuery.Among(typed, found, reach.Profile.Instance) is { } named)
                {
                    _ = reach.Open(Opening(named));

                    return;
                }

                reach.Push(new SearchResultsScreen(typed, found));
            });
    }

    /// <summary>What a direct query found opens as — the subject picking the same result opens.</summary>
    private static Subject Opening(DirectQuery.Named named) => named switch
    {
        DirectQuery.Named.OfAccount(var account) => SearchResultsScreen.Opening(account),
        DirectQuery.Named.OfPost(var post) => new Subject.Thread(post),
        _ => throw new ArgumentOutOfRangeException(nameof(named), named, "Not something a direct query opens."),
    };

    /// <summary>
    ///     What a search found, with the accounts among it carrying where the reader stands with them — two calls
    ///     under one enquiry, so the results paint once with the standings already in place rather than going up and
    ///     being decorated a moment later (#204).
    /// </summary>
    /// <remarks>
    ///     A run of no accounts costs nothing: the port answers an empty ask with its own input before it reaches an
    ///     instance, so a hashtag-only or post-only search makes the one call it always made. A run that was not
    ///     asked for at all stays <see langword="null" /> rather than becoming empty — that distinction is the whole
    ///     reason <see cref="SearchResults" /> exists.
    ///     <para>
    ///         Unchunked, unlike the follow list's, because a search's accounts run cannot outgrow one ask: Mastodon
    ///         caps <c>/api/v2/search</c> at 40 of each kind and the relationships endpoint takes 80 ids. Nothing in
    ///         this client enforces that cap, so a future instance that served more would make this two calls rather
    ///         than one — which costs a call and never a wrong row.
    ///     </para>
    /// </remarks>
    private static async Task<SearchResults> Searched(Enquiry.Ask ask, Reach reach, SearchQuery query)
    {
        var found = await ask.Of(token => reach.Ports.Search.Find(reach.Profile, query, token));

        return found.Accounts is { } accounts
            ? found with { Accounts = await ask.Of(token => reach.Ports.StoodOrSilent(reach.Profile, accounts, token)) }
            : found;
    }

    /// <summary>
    ///     What this screen's own <c>⏎</c> does, with a trend picked — the field takes its own: a trending hashtag opens
    ///     its timeline and a trending post its thread, as a search's results open them. A trending link does nothing
    ///     beyond being picked until a link can be opened (#607).
    /// </summary>
    public override Task Answer(Verb verb, Reach reach)
    {
        if (verb != Verb.OpenResult)
        {
            return Task.CompletedTask;
        }

        // A post opens through Opens rather than straight off the trend, as on every screen holding posts.
        if (Picked is not null)
        {
            return Opens is { } post ? reach.Open(new Subject.Thread(post)) : Task.CompletedTask;
        }

        return _walk.Out is Result.OfHashtag hashtag && SearchResultsScreen.Opening(hashtag) is { } opening
            ? reach.Open(opening)
            : Task.CompletedTask;
    }

    /// <inheritdoc />
    public override bool Heard(Change change)
    {
        switch (change)
        {
            case Change.PostChanged(var post):
                _walk.Rewrite(found =>
                    found is Result.OfPost(var held) ? new Result.OfPost(PostChange.Replaced(held, post)) : found);

                break;

            case Change.PostGone(var id):
                _walk.Remove(found => found is Result.OfPost(var held) && PostChange.Names(held, id));

                break;
        }

        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     A blank row, the field's box, and the trends under it in their headed sections — or the one sentence that
    ///     says there are none. Until the trends are read there is the field alone, usable at once.
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;
        var room = Stamped.Room(width);

        var lines = new List<Line>(Field(room));

        if (_read && _walk.Count == 1)
        {
            lines.Add(Line.Blank);
            lines.Add(Unstamped(Line.Of(TextWrap.Clip("Nothing trending on this instance", room), Role.Muted)));

            return lines;
        }

        for (var at = 1; at < _walk.Count; at++)
        {
            var trend = _walk.All[at];

            if (at == 1 || KindOf(trend) != KindOf(_walk.All[at - 1]))
            {
                lines.Add(Line.Blank);
                lines.Add(Unstamped(TrendLines.Heading(KindOf(trend), room)));
                lines.Add(Line.Blank);
            }
            else
            {
                lines.AddRange(Between(trend));
            }

            lines.AddRange(_walk.RowsOf(at, width, (found, where, inside) => found switch
            {
                Result.OfHashtag(var hashtag) => [TrendLines.Hashtag(hashtag, inside)],
                Result.OfLink(var link) => TrendLines.Link(link, inside),
                Result.OfPost(var post) => PostLines.Feed(post, drawing.In(inside), ReadingOf(post, at)),
                _ => [],
            }));
        }

        return lines;
    }

    /// <summary>
    ///     The field's box under a blank row, <paramref name="room" /> columns wide behind a gutter it leaves blank:
    ///     titled with the instance acted as, lit while the field takes letters and dimmed while it does not, with the
    ///     glass ahead of where the field is laid (<see cref="FieldAt" />).
    /// </summary>
    /// <remarks>
    ///     No selection bar or band beside it — the field marks its own focus — but its rows still say it is the thing
    ///     picked out where it is, by a mark that draws nothing, so that the page follows it and <c>[</c> and
    ///     <c>]</c> move from it as from anything picked (<see cref="Sections" />).
    /// </remarks>
    private IReadOnlyList<Line> Field(int room)
    {
        var inner = Math.Max(0, room - 2);
        var border = OnTheField ? Role.PanelBorderActive : Role.PanelBorder;
        var title = TextWrap.Clip($"─ {Instance} ", inner);

        Span[] gutter = OnTheField ? [new Span(" ", Role.Body), new Span("", Role.Selection)] : [new Span(" ", Role.Body)];

        IReadOnlyList<Line> box =
        [
            // A blank above the box, so that it stands clear of the panel's edge — the field's own row, so that the page
            // following the field back up brings it onto the page too.
            Line.Blank,
            Line.Of([
                new Span("╭" + title, border),
                new Span(new string('─', Math.Max(0, inner - Glyphs.Columns(title))) + "╮", border),
            ]),
            Line.Of([
                new Span("│", border),
                new Span(" ⌕ ", Role.Muted),
                new Span(new string(' ', Math.Max(0, inner - 3)), Role.Body),
                new Span("│", border),
            ]),
            Line.Of([new Span("╰" + new string('─', inner) + "╯", border)]),
        ];

        return [.. box.Select(line => line.After(gutter).PartOf(0))];
    }

    /// <summary>A row that is part of nothing, behind a blank gutter so that it lines up with the rows that are.</summary>
    private static Line Unstamped(Line line) => line.After(new Span(" ", Role.Body));

    /// <summary>
    ///     What stands between two trends of the same kind: nothing between hashtags, a blank between links, which are
    ///     three rows apiece and run together without one, and the rule a feed puts between posts.
    /// </summary>
    private static IReadOnlyList<Line> Between(Result trend) => trend switch
    {
        Result.OfHashtag => [],
        Result.OfLink => [Line.Blank],
        _ => [Line.Rule()],
    };

    /// <summary>Which kind of trend <paramref name="trend" /> is.</summary>
    private static TrendKind KindOf(Result trend) => trend switch
    {
        Result.OfHashtag => TrendKind.Hashtags,
        Result.OfLink => TrendKind.Links,
        _ => TrendKind.Posts,
    };
}
