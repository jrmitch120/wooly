using Wooly.Core.Search;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     The prompt <c>/</c> opens. A rail destination like any other, which is why <c>/</c> is a frame key: it means "go to
///     search" everywhere, and this is what it arrives at (<c>docs/tui-shell.md</c>).
/// </summary>
/// <remarks>
///     The prompt alone: what a search finds is pushed over it as a screen of its own
///     (<see cref="SearchResultsScreen" />), and a <see cref="DirectQuery" /> opens what it names straight over it — so
///     <c>esc</c> from either comes back here with the query as it was typed, for refining rather than retyping (#600).
///     Choosing Search on the rail, or <c>/</c>, arrives at a fresh one, as choosing any destination resets the stack.
///     <para>
///         What has been typed lives here rather than in a widget, for the reason ADR-0015 gives about the compose
///         editor: what is being searched for is then a fact about the shell — something a test can set and read —
///         rather than something only a terminal knows.
///     </para>
/// </remarks>
public sealed class SearchScreen : Screen
{
    /// <inheritdoc />
    public override string Crumb => "Search";

    /// <inheritdoc />
    /// <remarks>
    ///     Only the keys that are not letters, because every other one goes into the query rather than acting on
    ///     anything — including <c>/</c> and <c>?</c>, which a web address and a question are both entitled to contain.
    ///     Nothing to walk, either: what is on screen is the prompt and the sentence under it, so the arrows are left
    ///     unsaid rather than offered against three rows.
    ///     <para>
    ///         No <c>esc</c>: search is a rail destination, so the prompt is the bottom of the stack and there is
    ///         nothing under it to walk back to. <c>tab</c> is how you leave, the same as on every other destination.
    ///     </para>
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys => [new KeyHint("⏎", "search"), PostKeys.ToADestination];

    /// <inheritdoc />
    /// <remarks>Always: the prompt is what this screen is, and the results that would stop it are a screen of their own.</remarks>
    public override bool IsTyping => true;

    /// <summary>What has been typed so far, which is what <c>⏎</c> asks the instance for.</summary>
    public string Query { get; private set; } = string.Empty;

    /// <inheritdoc />
    /// <remarks>Into the query, which is the only thing this screen takes letters into.</remarks>
    public override void Type(char letter) => Query += letter;

    /// <inheritdoc />
    public override void Backspace() => Query = Backspaced(Query);

    /// <summary>
    ///     What this screen's own <c>⏎</c> does: asks the instance for what has been typed — or opens it straight away,
    ///     where it is a <see cref="DirectQuery" />.
    /// </summary>
    public override Task Answer(Verb verb, Reach reach) => verb == Verb.Find ? Find(reach) : Task.CompletedTask;

    /// <summary>Asks the instance for what has been typed into the prompt.</summary>
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
    private Task Find(Reach reach)
    {
        if (!SearchQuery.IsWellFormed(Query))
        {
            // The same words the command turns an empty query down with, so that the two front ends cannot come to
            // say different things about the same empty value.
            reach.Say(SearchQuery.Rejection, isError: true);

            return Task.CompletedTask;
        }

        // Nothing to ask the instance where what was typed names a tag, since any well-formed tag has a timeline. It
        // is pushed exactly as picking it from the results would push it, and this screen is left as it was — the
        // prompt, still taking letters — for esc to come back to.
        if (DirectQuery.Tag(Query) is { } tag)
        {
            return reach.Open(new Subject.Tag(tag));
        }

        // A handle or an address asks the one search it always asked, an address in full so that it is resolved
        // rather than searched for as words. Where it found exactly what was named, that is opened straight over the
        // prompt as picking it out would open it, with no results beneath it — so one esc comes back to what was typed
        // (#600). Anything else pushes its results, the near miss among them.
        var typed = Query.Trim();
        var query = SearchQuery.For(
            DirectQuery.Asking(typed),
            page: SearchPage.Of(limit: SearchResultsScreen.PerKind));

        return reach.Put(
            ask => Searched(ask, reach, query),
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

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;

        return
        [
            Prompt(width),
            Line.Blank,
            Line.Of(
                TextWrap.Clip("A word, a #hashtag, an @account, or the web address of one of them.", width),
                Role.Muted),
        ];
    }

    /// <summary>
    ///     What is being searched for, with the caret where the next letter lands. The caret is a mark rather than a
    ///     colour, so a terminal with none still says where the typing is going.
    /// </summary>
    private Line Prompt(int width)
    {
        const string label = "Search: ";
        var typed = TextWrap.Clip(Query, Math.Max(0, width - Glyphs.Columns(label) - 1));

        return Line.Of([new Span(label, Role.Muted), new Span(typed, Role.Body), new Span("▌", Role.Selection)]);
    }
}
