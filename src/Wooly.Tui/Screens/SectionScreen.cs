using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Core.Trends;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One <b>Section</b> of a screen pushed on its own and <b>read on</b> (GLOSSARY.md): the posts a search found,
///     say, opened from their heading on the mixed results and crumbed <c>Search › cats › Posts</c>, where the mixed
///     results hold a preview of twenty (#604) — or one kind of <b>Trend</b>, opened from its heading under the empty
///     prompt and crumbed <c>Search › Trending posts</c>, where the prompt shows ten, or three (#606).
/// </summary>
/// <remarks>
///     One kind of thing, drawn exactly as the section it was opened from draws it (<see cref="ResultLines" />), so a
///     hashtag read on to past the twentieth looks like the twenty above it — and walked, opened and acted on as there:
///     <c>⏎</c> opens what is picked, and the post keys act on a post as they do on any feed.
///     <para>
///         Nothing here fetches: what was read arrives whole when the screen goes up, and the rest through
///         <see cref="ReadOn" /> as the page nears the end of what is in hand — however its subject pages, which for a
///         narrowed search, and for a kind of trend, is by offset (<see cref="Core.Paging.OffsetReading" />).
///     </para>
/// </remarks>
public sealed class SectionScreen : Screen, IReadsOn<Result>
{
    private readonly Picked<Result> _results;

    /// <summary>Whether what the section holds is a kind of trend, rather than a kind a search found.</summary>
    private readonly bool _trending;

    /// <summary>A section of <paramref name="kind" /> holding <paramref name="results" />, so far.</summary>
    /// <param name="kind">What the section is of, which is what it is crumbed with.</param>
    /// <param name="results">What its first read brought.</param>
    /// <param name="nothing">What it says where its first read brought nothing.</param>
    public SectionScreen(SearchKind kind, IReadOnlyList<Result> results, string nothing)
        : this(Capitalised(ResultLines.Called(kind)), results, nothing, trending: false)
    {
    }

    /// <summary>
    ///     A section of the trends of <paramref name="kind" /> holding <paramref name="trends" />, so far (#606).
    /// </summary>
    /// <param name="kind">What kind of trend it is of, which is what it is crumbed with.</param>
    /// <param name="trends">What its first read brought.</param>
    /// <param name="nothing">What it says where its first read brought nothing.</param>
    public SectionScreen(TrendKind kind, IReadOnlyList<Result> trends, string nothing)
        : this(TrendLines.Called(kind), trends, nothing, trending: true)
    {
    }

    private SectionScreen(string crumb, IReadOnlyList<Result> results, string nothing, bool trending)
    {
        Crumb = crumb;
        Nothing = nothing;
        _trending = trending;
        _results = new Picked<Result>(results);
    }

    /// <summary>What it says where its first read brought nothing.</summary>
    public string Nothing { get; }

    /// <inheritdoc />
    /// <remarks>
    ///     What the section is of, as the heading it was opened from calls it — the crumb under it being the screen it
    ///     was opened from, so the breadcrumb reads <c>Search › cats › Posts</c>, or <c>Search › Trending posts</c>.
    /// </remarks>
    public override string Crumb { get; }

    /// <inheritdoc />
    /// <remarks>
    ///     The keys the section offered on the screen it was opened from, but for <c>[</c> and <c>]</c>, there being one
    ///     section here and nowhere to jump to — and <c>⏎</c> only where what is picked opens, which a trending link
    ///     does not yet (#607).
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys
    {
        get
        {
            var moving = new KeyHint("j/k", _trending ? "trend" : "result");
            var searching = new KeyHint("/", _trending ? "search" : "search again");

            return Picked is not null
                ? PostKeys.Around(moving, [searching], PostKeys.AlongTheRail)
                :
                [
                    moving,
                    .. _results.Out is Result.OfLink
                        ? Array.Empty<KeyHint>()
                        : [new KeyHint("⏎", "open", NeedsAPick: true)],
                    searching,
                    .. PostKeys.Leaving(PostKeys.AlongTheRail),
                ];
        }
    }

    /// <summary>Which result is picked out.</summary>
    public int At => _results.At;

    /// <summary>Everything read so far, in the order it was read.</summary>
    public IReadOnlyList<Result> Items => _results.All;

    /// <summary>The accounts held, where the section is of accounts.</summary>
    public IReadOnlyList<Account> Accounts => [.. _results.All.OfType<Result.OfAccount>().Select(one => one.Account)];

    /// <summary>The hashtags held, where the section is of hashtags.</summary>
    public IReadOnlyList<Hashtag> Hashtags => [.. _results.All.OfType<Result.OfHashtag>().Select(one => one.Hashtag)];

    /// <summary>The posts held, where the section is of posts.</summary>
    public IReadOnlyList<Post> Posts => [.. _results.All.OfType<Result.OfPost>().Select(one => one.Post)];

    /// <summary>The trending links held, where the section is of them.</summary>
    public IReadOnlyList<TrendingLink> Links => [.. _results.All.OfType<Result.OfLink>().Select(one => one.Link)];

    /// <inheritdoc />
    public override IEnumerable<Mentionable> Seen => [.. Accounts.Select(Mentionable.Of), .. Mentionable.In(Posts)];

    /// <inheritdoc />
    /// <remarks>The post picked out, so the post keys mean here what they mean on a feed.</remarks>
    public override Post? Picked => _results.Out is Result.OfPost(var post) ? post : null;

    /// <inheritdoc />
    protected override IPicked Walking => _results;

    /// <inheritdoc />
    /// <remarks>
    ///     Under everything held, less anything already held: the instance ranks a search — and its trends — afresh for
    ///     every page, so a thing can come back on the page after the one it was first read on, and drawn twice it would
    ///     read as two.
    /// </remarks>
    public void ReadOn(IReadOnlyList<Result> older) =>
        _results.PutUnder(older.Where(result => !_results.All.Any(held => held.IsSameAs(result))));

    /// <summary>What this screen's own <c>⏎</c> does: opens what is picked out, as on the screen it was opened from.</summary>
    public override Task Answer(Verb verb, Reach reach)
    {
        if (verb != Verb.OpenResult)
        {
            return Task.CompletedTask;
        }

        // A post opens through Opens rather than straight off the result, as on every screen holding posts.
        if (Picked is not null)
        {
            return Opens is { } post ? reach.Open(new Subject.Thread(post)) : Task.CompletedTask;
        }

        return _results.Out is { } result && SearchResultsScreen.Opening(result) is { } opening
            ? reach.Open(opening)
            : Task.CompletedTask;
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

        if (_results.Count == 0)
        {
            // No foot: a section with nothing in it is whole, and the sentence already says so.
            return [Line.Of(TextWrap.Clip(Nothing, width), Role.Muted)];
        }

        // A trending hashtag is drawn as under the prompt, its count against the right edge, and needs no columns.
        var columns = _trending ? default : ResultLines.Columns.Of(Hashtags, Stamped.Room(width));
        var lines = new List<Line>();

        for (var at = 0; at < _results.Count; at++)
        {
            if (at > 0)
            {
                lines.AddRange(ResultLines.Between(_results.All[at]));
            }

            lines.AddRange(_results.RowsOf(at, width, (found, where, room) => found switch
            {
                // Drawn exactly as under the prompt they were opened from, so the eleventh reads like the ten above it.
                Result.OfHashtag(var hashtag) when _trending => [TrendLines.Hashtag(hashtag, room)],
                Result.OfLink(var link) => TrendLines.Link(link, room),
                _ => ResultLines.Of(found, where, room, drawing, columns, this),
            }));
        }

        // Set apart from the last of them as they are set apart from one another, but for hashtags, whose one-row
        // runs take nothing between them and would run into the foot.
        var apart = ResultLines.Between(_results.All[^1]) is [var between] ? between : Line.Blank;

        return WithFoot(lines, apart);
    }

    /// <summary><paramref name="called" /> with its first letter capitalised, as a crumb is written.</summary>
    private static string Capitalised(string called) =>
        string.Concat(char.ToUpperInvariant(called[0]).ToString(), called[1..]);
}
