using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Core.Trends;

namespace Wooly.Tui.Screens;

/// <summary>
///     One thing a search turned up, as the screen walks them: an account, a hashtag, or a post — or the heading over a
///     run of one of the three, which is walked to and opened like any of them (#604). And what the empty prompt walks
///     besides: its own field, at the top, and the <b>Trend</b>s under it — a trending hashtag or post being a hashtag
///     or a post like any search finds, and a <b>Trending link</b> the one kind of its own (#605).
/// </summary>
/// <remarks>
///     One list rather than the three <see cref="SearchResults" /> holds, because what <c>j</c> and <c>k</c> walk is
///     one list — and saying so outright is what stops the order the results are drawn in and the order they are
///     picked out in drifting apart.
///     <para>
///         A shape this screen makes rather than one the domain has, which is why it lives here and not in
///         <c>Wooly.Core</c>: the CLI reads a search in its three kinds and has no selection to walk at all, so
///         flattening them is a navigation fact about one front end. ADR-0011's one ask and three kinds is untouched.
///     </para>
/// </remarks>
public abstract record Result
{
    /// <remarks>
    ///     Closed, so that the three kinds a search has, and the heading over a run of each, are the kinds a row can be
    ///     drawn from.
    /// </remarks>
    private Result()
    {
    }

    /// <summary>An account the search found.</summary>
    public sealed record OfAccount(Account Account) : Result;

    /// <summary>A hashtag the search found.</summary>
    public sealed record OfHashtag(Hashtag Hashtag) : Result;

    /// <summary>A post the search found.</summary>
    public sealed record OfPost(Post Post) : Result;

    /// <summary>
    ///     A <b>Trending link</b> under the empty prompt (#605): picked like any trend, and opened as the posts sharing it
    ///     once a link can be (#607).
    /// </summary>
    public sealed record OfLink(TrendingLink Link) : Result;

    /// <summary>
    ///     The search field, at the top of the empty prompt's walk (#605): picked, it takes letters; walked off, they are
    ///     keys again. In the walk rather than beside it, so that <c>j</c>, <c>k</c>, a click and <c>[</c> move between
    ///     it and the trends under it as they move between any two things on a screen.
    /// </summary>
    public sealed record Field : Result;

    /// <summary>
    ///     The heading over the run of <paramref name="Kind" /> a search found: in the walk rather than only drawn, so
    ///     that it can be picked out and opened — which pushes that kind alone, read on past what the mixed results hold
    ///     (#604).
    /// </summary>
    /// <param name="Kind">Which kind of result the run under it is.</param>
    public sealed record Section(SearchKind Kind) : Result;

    /// <summary>
    ///     Whether this is the same thing as <paramref name="other" />, as the instance names it — the same account, the
    ///     same tag, the same post — however what it says of it has changed between two answers.
    /// </summary>
    /// <remarks>
    ///     What a list read on by offset is kept free of repeats by: the instance ranks a search afresh for every page,
    ///     so a thing can come back on the page after the one it was first found on (#604).
    /// </remarks>
    public bool IsSameAs(Result other) => (this, other) switch
    {
        (OfAccount(var one), OfAccount(var two)) => one.Id == two.Id,
        (OfHashtag(var one), OfHashtag(var two)) => string.Equals(one.Name, two.Name, StringComparison.OrdinalIgnoreCase),
        (OfPost(var one), OfPost(var two)) => one.Id == two.Id,
        (OfLink(var one), OfLink(var two)) => one.Url == two.Url,
        (Field, Field) => true,
        (Section(var one), Section(var two)) => one == two,
        _ => false,
    };
}
