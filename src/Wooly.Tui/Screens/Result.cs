using Wooly.Core.Accounts;
using Wooly.Core.Posts;
using Wooly.Core.Search;
using Wooly.Core.Trends;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Screens;

/// <summary>
///     One thing a search turned up, as the screen walks them: an account, a hashtag, or a post — or the heading over a
///     run of one of the three, which is walked to and opened like any of them (#604). And what the empty prompt walks
///     besides: its own field, at the top, and the <b>Trend</b>s under it — a trending hashtag or post being a hashtag
///     or a post like any search finds, and a <b>Trending link</b> the one kind of its own (#605) — and the heading over
///     each run of them, walked to and opened as a search's are (#606).
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
///     <para>
///         What every screen walking them does alike lives here, once: what one opens as (<see cref="Opening()" />),
///         what <c>⏎</c> and <c>o</c> do with the one picked (<see cref="Answer" />), a run under its heading
///         (<see cref="Headed" />), and the walk taking in a change to a post on it (<see cref="Hear" />) — rather than
///         on the prompt, the results and the screen of one kind each (#598's review).
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
    ///     (#607).
    /// </summary>
    public sealed record OfLink(TrendingLink Link) : Result;

    /// <summary>
    ///     The search field, at the top of the empty prompt's walk (#605): picked, it takes letters; walked off, they are
    ///     keys again. In the walk rather than beside it, so that <c>j</c>, <c>k</c>, a click and <c>[</c> move between
    ///     it and the trends under it as they move between any two things on a screen.
    /// </summary>
    public sealed record Field : Result;

    /// <summary>
    ///     The heading over the run of <paramref name="Kind" /> a search for <paramref name="Asked" /> found: in the walk
    ///     rather than only drawn, so that it can be picked out and opened — which pushes that kind alone, read on past
    ///     what the mixed results hold (#604).
    /// </summary>
    /// <param name="Asked">What was searched for, as it was typed: what the kind alone is asked again for.</param>
    /// <param name="Kind">Which kind of result the run under it is.</param>
    public sealed record Section(string Asked, SearchKind Kind) : Result;

    /// <summary>
    ///     The heading over the run of <paramref name="Kind" /> under the empty prompt: in the walk for the reason a
    ///     search's heading is, so that it can be picked out and opened — which pushes that kind of trend alone, read on
    ///     past the few the prompt shows (#606).
    /// </summary>
    /// <param name="Kind">Which kind of trend the run under it is.</param>
    public sealed record TrendSection(TrendKind Kind) : Result;

    /// <summary>
    ///     Whether this is a heading over a run rather than a thing in one — picked out only to be opened, which is why a
    ///     click on one opens it as <c>⏎</c> would.
    /// </summary>
    public bool Heads => this is Section or TrendSection;

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
        _ => this == other,
    };

    /// <summary>
    ///     What this opens as, wherever it was found — on a search's results, under the prompt, or on the screen of its
    ///     kind alone: an account, a hashtag's timeline, a post's thread, the posts sharing a <b>Trending link</b> (#607),
    ///     or for a heading that kind alone, read on (#604, #606). Nothing for the field, which takes letters instead.
    /// </summary>
    /// <remarks>
    ///     A hashtag opens as a screen on the stack rather than as a place on the rail. Which tags the rail keeps places
    ///     for are the tag sets the reader wrote down (<c>docs/tui-shell.md</c>), and a search result is not them changing
    ///     their mind about them.
    /// </remarks>
    internal Subject? Opening() => this switch
    {
        OfAccount(var account) => Opening(account),
        OfHashtag(var hashtag) => new Subject.Tag(hashtag.Name),
        OfPost(var post) => new Subject.Thread(post),
        OfLink(var link) => new Subject.Sharing(link),
        Section(var asked, var kind) => new Subject.Narrowed(asked, kind),
        TrendSection(var kind) => new Subject.TrendsOf(kind),
        _ => null,
    };

    /// <summary>What an account a search found opens as, whether it was picked out of the results or named at the prompt.</summary>
    internal static Subject Opening(Account account) =>
        new Subject.Account(AccountAddress.Parse(account.Address), WithReplies: false);

    /// <summary>
    ///     What <paramref name="verb" /> does with <paramref name="picked" /> on a screen walking results: <c>⏎</c> opens
    ///     it (<see cref="Opening()" />), and <c>o</c> on a trending link opens the article itself, in the browser (#607).
    /// </summary>
    /// <param name="verb">What was asked for.</param>
    /// <param name="reach">What the screen can reach.</param>
    /// <param name="picked">What is picked out on the screen, if anything.</param>
    /// <param name="opens">
    ///     The post the screen opens (<see cref="Screen.Opens" />): a post opens through it rather than straight off the
    ///     result, as on every screen holding posts.
    /// </param>
    internal static Task Answer(Verb verb, Reach reach, Result? picked, Post? opens)
    {
        if (verb == Verb.OpenArticle && picked is OfLink(var article))
        {
            reach.Browse(article.Url.OriginalString);

            return Task.CompletedTask;
        }

        if (verb != Verb.OpenResult)
        {
            return Task.CompletedTask;
        }

        if (picked is OfPost)
        {
            return opens is { } post ? reach.Open(new Subject.Thread(post)) : Task.CompletedTask;
        }

        return picked?.Opening() is { } opening ? reach.Open(opening) : Task.CompletedTask;
    }

    /// <summary>
    ///     <paramref name="run" /> under <paramref name="heading" />, or nothing at all where the run is empty — a run with
    ///     nothing in it draws no heading, and so is no section.
    /// </summary>
    internal static IEnumerable<Result> Headed(Result heading, IEnumerable<Result> run)
    {
        var held = run.ToList();

        return held.Count == 0 ? [] : [heading, .. held];
    }

    /// <summary>
    ///     <paramref name="walk" /> taking in <paramref name="change" />: a post on it changed is drawn changed, and one gone
    ///     is gone — the rest of a walk being nothing a change to a post touches.
    /// </summary>
    internal static void Hear(Picked<Result> walk, Change change)
    {
        switch (change)
        {
            case Change.PostChanged(var post):
                walk.Rewrite(found =>
                    found is OfPost(var held) ? new OfPost(PostChange.Replaced(held, post)) : found);

                break;

            case Change.PostGone(var id):
                walk.Remove(found => found is OfPost(var held) && PostChange.Names(held, id));

                break;
        }
    }
}
