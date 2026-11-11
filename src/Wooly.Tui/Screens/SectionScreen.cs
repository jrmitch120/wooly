using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     One <b>Section</b> of a screen pushed on its own and <b>read on</b> (GLOSSARY.md): the posts a search found,
///     say, opened from their heading on the mixed results and crumbed <c>Search › cats › Posts</c>, where the mixed
///     results hold a preview of twenty (#604).
/// </summary>
/// <remarks>
///     One kind of thing, drawn exactly as the section it was opened from draws it (<see cref="ResultLines" />), so a
///     hashtag read on to past the twentieth looks like the twenty above it — and walked, opened and acted on as there:
///     <c>⏎</c> opens what is picked, and the post keys act on a post as they do on any feed.
///     <para>
///         Nothing here fetches: what was read arrives whole when the screen goes up, and the rest through
///         <see cref="ReadOn" /> as the page nears the end of what is in hand — however its subject pages, which for a
///         narrowed search is by offset (<see cref="Core.Paging.OffsetReading" />).
///     </para>
/// </remarks>
public sealed class SectionScreen : Screen, IReadsOn<Result>
{
    private readonly Picked<Result> _results;

    /// <summary>A section of <paramref name="kind" /> holding <paramref name="results" />, so far.</summary>
    /// <param name="kind">What the section is of, which is what it is crumbed with.</param>
    /// <param name="results">What its first read brought.</param>
    /// <param name="nothing">What it says where its first read brought nothing.</param>
    public SectionScreen(SearchKind kind, IReadOnlyList<Result> results, string nothing)
    {
        Kind = kind;
        Nothing = nothing;
        _results = new Picked<Result>(results);
    }

    /// <summary>What the section is of.</summary>
    public SearchKind Kind { get; }

    /// <summary>What it says where its first read brought nothing.</summary>
    public string Nothing { get; }

    /// <inheritdoc />
    /// <remarks>
    ///     What the section is of, capitalised — the crumb under it being the screen it was opened from, so the
    ///     breadcrumb reads <c>Search › cats › Posts</c>.
    /// </remarks>
    public override string Crumb
    {
        get
        {
            var called = ResultLines.Called(Kind);

            return string.Concat(char.ToUpperInvariant(called[0]).ToString(), called[1..]);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The keys the section offered on the screen it was opened from, but for <c>[</c> and <c>]</c>, there being one
    ///     section here and nowhere to jump to.
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
        Picked is not null
            ? PostKeys.Around(
                new KeyHint("j/k", "result"),
                [new KeyHint("/", "search again")],
                PostKeys.AlongTheRail)
            :
            [
                new KeyHint("j/k", "result"),
                new KeyHint("⏎", "open", NeedsAPick: true),
                new KeyHint("/", "search again"),
                .. PostKeys.Leaving(PostKeys.AlongTheRail),
            ];

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

    /// <inheritdoc />
    public override IEnumerable<Mentionable> Seen => [.. Accounts.Select(Mentionable.Of), .. Mentionable.In(Posts)];

    /// <inheritdoc />
    /// <remarks>The post picked out, so the post keys mean here what they mean on a feed.</remarks>
    public override Post? Picked => _results.Out is Result.OfPost(var post) ? post : null;

    /// <inheritdoc />
    protected override IPicked Walking => _results;

    /// <inheritdoc />
    /// <remarks>
    ///     Under everything held, less anything already held: the instance ranks a search afresh for every page, so a
    ///     thing can come back on the page after the one it was first read on, and drawn twice it would read as two.
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

        var columns = ResultLines.Columns.Of(Hashtags, Stamped.Room(width));
        var lines = new List<Line>();

        for (var at = 0; at < _results.Count; at++)
        {
            if (at > 0)
            {
                lines.AddRange(ResultLines.Between(_results.All[at]));
            }

            lines.AddRange(_results.RowsOf(at, width, (found, where, room) =>
                ResultLines.Of(found, where, room, drawing, columns, this)));
        }

        // Set apart from the last of them as they are set apart from one another, but for hashtags, whose one-row
        // runs take nothing between them and would run into the foot.
        var apart = ResultLines.Between(_results.All[^1]) is [var between] ? between : Line.Blank;

        return WithFoot(lines, apart);
    }
}
