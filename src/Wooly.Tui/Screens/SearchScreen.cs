using System.Drawing;
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
///         What has been typed is not held here: it is a real one-line field's, laid over the prompt's row as compose's
///         content warning is laid over its header (#320, ADR-0030), so that it has a caret that moves, a selection, and
///         copy, cut and paste (#608). The prompt says where the field goes (<see cref="FieldAt" />) and, given what the
///         field held when <c>⏎</c> was pressed in it, what asking for it does (<see cref="Find" />).
///     </para>
/// </remarks>
public sealed class SearchScreen : Screen
{
    /// <summary>What the prompt writes ahead of its field, on the field's row.</summary>
    private const string Label = "Search: ";

    /// <inheritdoc />
    public override string Crumb => "Search";

    /// <inheritdoc />
    /// <remarks>
    ///     Only the keys that are not letters, because every other one goes into the field rather than acting on
    ///     anything — including <c>/</c> and <c>?</c>, which a web address and a question are both entitled to contain.
    ///     Nothing to walk, either: what is on screen is the prompt and the sentence under it, so the arrows are left
    ///     unsaid rather than offered against three rows.
    ///     <para>
    ///         No <c>esc</c>: search is a rail destination, so the prompt is the bottom of the stack and there is
    ///         nothing under it to walk back to. <c>tab</c> is how you leave, the same as on every other destination.
    ///     </para>
    ///     <para>
    ///         <c>ctrl-u</c> only while the field holds something to empty (#310).
    ///     </para>
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        new KeyHint("⏎", "search"),
        .. HoldsAQuery ? [PostKeys.Clearing] : Array.Empty<KeyHint>(),
        PostKeys.ToADestination,
    ];

    /// <summary>
    ///     Whether the field holds anything — which is all the prompt knows of what has been typed, and all it needs to:
    ///     whether <c>ctrl-u</c> has something to empty, and so whether the status row offers it (#310).
    /// </summary>
    public bool HoldsAQuery { get; private set; }

    /// <summary>The field now holds <paramref name="query" />: told by the field as it changes.</summary>
    /// <returns>Whether that changed whether it holds anything, and so the keys the status row offers.</returns>
    public bool Written(string query)
    {
        var holds = query.Length > 0;

        if (holds == HoldsAQuery)
        {
            return false;
        }

        HoldsAQuery = holds;

        return true;
    }

    /// <summary>
    ///     Where the field goes on a prompt whose rows are drawn <paramref name="width" /> columns wide: the rest of the
    ///     first row, after <see cref="Label" />.
    /// </summary>
    public Rectangle FieldAt(int width)
    {
        var label = Glyphs.Columns(Label);

        return new Rectangle(label, 0, Math.Max(0, width - label), 1);
    }

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

    /// <inheritdoc />
    /// <remarks>
    ///     The label alone on the first row: the field is laid over the rest of it (<see cref="FieldAt" />).
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;

        return
        [
            Line.Of(TextWrap.Clip(Label, width), Role.Muted),
            Line.Blank,
            Line.Of(
                TextWrap.Clip("A word, a #hashtag, an @account, or the web address of one of them.", width),
                Role.Muted),
        ];
    }}
