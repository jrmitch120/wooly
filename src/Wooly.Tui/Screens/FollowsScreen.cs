using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Relationships;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     Everyone an account follows, or everyone who follows it. One screen for both sides and for anybody's account:
///     <c>w</c> opens the following side and <c>s</c> swaps to the other in place (<c>docs/tui-shell.md</c>, #180).
/// </summary>
/// <remarks>
///     Two modes, and which one is settled before anything is fetched: an <see cref="Account" /> already carries both
///     counts, so a list this client can hold whole is held whole and one it cannot is browsed a page at a time —
///     rather than discovering hundreds of calls in that <c>@Mastodon@mastodon.social</c> has 877,000 followers.
///     <para>
///         The filter belongs to the held mode alone. Narrowing what has already been read is the dishonest filter
///         #162 ruled out: 240 read of 877,000 makes "nobody matches" meaningless, so the browsed mode is offered no
///         filter at all rather than one that lies. Refusing to open the screen was rejected too — walking the first
///         few hundred of anyone's followers is a real thing to do, on the same rows with the same <c>⏎</c>.
///     </para>
///     <para>
///         Nothing here fetches. What is read is the shell's, and arrives through <see cref="Arrived" /> — so the
///         whole of this screen can be walked, filtered and drawn with no terminal and no instance.
///     </para>
/// </remarks>
public sealed class FollowsScreen : Screen
{
    /// <summary>Everyone read so far, in the order the instance listed them.</summary>
    private readonly List<Account> _all = [];

    /// <summary>Whoever the filter leaves, with one of them picked out — which is the whole of what the reader walks.</summary>
    /// <remarks>
    ///     Rebuilt rather than filtered where it is drawn, because the pick is an index into what is on screen: a
    ///     screen drawing eleven people and picking the fortieth is a screen with nothing drawn as picked out.
    /// </remarks>
    private Picked<Account> _walking = new([]);

    private bool _typing;

    /// <param name="whose">
    ///     The account whose list this is, as the screen it was opened from was holding them — which is where both
    ///     counts come from, and so where the mode comes from.
    /// </param>
    /// <param name="side">Which side of their follows is being listed.</param>
    /// <param name="mine">
    ///     Whether this is the reader's own account, which is what settles what the rows may leave unsaid: every row
    ///     of your own following list is somebody you follow, and saying so 187 times says nothing.
    /// </param>
    public FollowsScreen(Account whose, FollowSide side, bool mine)
    {
        Whose = whose;
        Side = side;
        Mine = mine;
        Total = side.Either(followers: whose.Followers, following: whose.Following);
    }

    /// <summary>The account whose list this is.</summary>
    public Account Whose { get; }

    /// <summary>Which side of their follows is on screen.</summary>
    public FollowSide Side { get; }

    /// <summary>Whether it is the reader's own account.</summary>
    public bool Mine { get; }

    /// <summary>
    ///     How many the instance says there are on this side, which is what the mode is settled by and what the count
    ///     row measures against.
    /// </summary>
    public long Total { get; }

    /// <summary>
    ///     Whether the whole list is held: fetched progressively and walkable from the first page, with a filter over
    ///     all of it. The other mode browses what has been read and asks for more as the reader reaches the end.
    /// </summary>
    public bool Holds => Total < ShellTiming.FollowsHeldUnder;

    /// <summary>Everyone read so far, whatever the filter is leaving out.</summary>
    public IReadOnlyList<Account> People => _all;

    /// <summary>Whoever is on screen, which is everyone read where no filter is narrowing them.</summary>
    public IReadOnlyList<Account> Shown => _walking.All;

    /// <summary>How many have been read, which is the first of the two numbers the count row says.</summary>
    public int Read => _all.Count;

    /// <summary>
    ///     Whether there is more of this list to come: in flight on a held one, and there to be asked for on a
    ///     browsed one. One question rather than two, because what a reader wants to know is the same either way —
    ///     that what is on screen is not the whole of it.
    /// </summary>
    /// <remarks>
    ///     Not called <c>Reading</c>, which this namespace has already spent on what a reader has done to one post
    ///     (CONTEXT.md).
    /// </remarks>
    public bool More { get; private set; }

    /// <summary>What is narrowing the list, or empty where nothing is.</summary>
    public string Filter { get; private set; } = string.Empty;

    /// <summary>
    ///     Whether the reader has walked onto the end of what has been read and there is more of it to ask for —
    ///     which is what <c>j</c> past the bottom means on a browsed list.
    /// </summary>
    /// <remarks>
    ///     Never on a held list: the whole of that one is already on its way, and asking again would be asking for
    ///     what is in flight.
    /// </remarks>
    public bool WantsMore => !Holds && More && Read > 0 && _walking.At >= _walking.Count - 1;

    /// <summary>
    ///     What this list is told where nobody is on it: that there is nobody, or — where the profile says otherwise
    ///     — that the instance listed nobody all the same.
    /// </summary>
    /// <remarks>
    ///     The two are different facts and must not be said with the same sentence. An account can keep who it
    ///     follows to itself, and Mastodon answers that by serving an empty list while the count on the profile goes
    ///     on saying five: telling a reader "they follow nobody" there would be reporting a setting as a fact. Both
    ///     numbers are said and no cause is guessed at, which is the same honesty an absent standing gets (#180).
    /// </remarks>
    public string Nobody => Total > 0
        ? $"{(Mine ? "Your" : "Their")} profile says {Number.Of(Total)} {Called}, but the instance listed nobody."
        : Side.Either(
            followers: Mine ? "Nobody follows you yet." : "Nobody follows them yet.",
            following: Mine ? "You follow nobody yet." : "They follow nobody yet.");

    /// <summary>
    ///     What the shell has to say about the list rather than about anybody on it — that nobody is on it, or that a
    ///     rate limit cut the reading short.
    /// </summary>
    /// <remarks>
    ///     Drawn on the screen rather than said on the status row, which is where every other list this shell draws
    ///     puts one: the row holds either a notice or the keys and never both, so a notice standing there is every
    ///     key the screen answers to, hidden — and on a list with nobody on it there is nothing to walk, so nothing
    ///     ever comes along to take it down again.
    /// </remarks>
    public string? Notice { get; private set; }

    /// <summary>Whoever is picked out, or nobody where nobody is on screen. What <c>⏎</c> opens.</summary>
    public Account? PickedPerson => _walking.Out;

    /// <inheritdoc />
    public override string Crumb => $"@{Whose.Address} {Called}";

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <inheritdoc />
    public override bool IsTyping => _typing;

    /// <inheritdoc />
    /// <remarks>
    ///     A row here is a person rather than a post, so this screen answers to none of the keys that act on one —
    ///     the same position the conversations list and the follow requests screen are already in.
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys => _typing
        ?
        [
            new KeyHint("⏎", "done"),
            new KeyHint("esc", "clear"),
        ]
        :
        [
            new KeyHint("j/k", "person"),
            new KeyHint("⏎", "open", NeedsAPick: true),
            // Not a key that needs somebody picked out (#195): what it opens is a prompt, and a filter that has
            // narrowed the list to nobody is exactly when a reader wants the key back.
            .. Holds ? new KeyHint[] { new("f", "filter") } : [],
            new KeyHint("s", Side.Either(followers: "following", following: "followers")),
            Refreshing,
            PostKeys.Scrolling,
            new KeyHint("esc", "back"),
            new KeyHint("?", "keys"),
        ];

    /// <inheritdoc />
    protected override IPicked Walking => _walking;

    /// <summary>What this side is called, which the crumb says and the count row measures.</summary>
    private string Called => Side.Either(followers: "followers", following: "following");

    /// <summary>
    ///     What a row on this list may not say, because the list has already said it of everyone on it. Nothing on
    ///     somebody else's, where the reader stands in no particular relation to anybody (#180).
    /// </summary>
    private Implied Implies => Mine
        ? Side.Either(followers: Implied.TheyFollowYou, following: Implied.YouFollowThem)
        : Implied.Nothing;

    /// <summary>
    ///     Puts what has been read on the screen: whoever of <paramref name="people" /> is not already here, in the
    ///     order they arrived.
    /// </summary>
    /// <remarks>
    ///     Only who is new, because a page is asked for by re-reading the list to a longer limit — the port takes a
    ///     count rather than a cursor — so every page arrives with every page before it in front of it. The pick does
    ///     not move: somebody reading the tenth person does not want to be somewhere else because the eleventh landed.
    /// </remarks>
    /// <param name="people">Everyone the read came back with, from the first of them.</param>
    /// <param name="more">Whether there is more still coming, which is what the count row says while it is.</param>
    /// <param name="notice">
    ///     What the shell has to say about the read — that it found nobody, or that a rate limit stopped it — or
    ///     nothing, which takes down whatever the last read left standing.
    /// </param>
    public void Arrived(IReadOnlyList<Account> people, bool more, string? notice = null)
    {
        More = more;
        Notice = notice;

        foreach (var person in people.Skip(_all.Count))
        {
            _all.Add(person);

            if (Matches(person))
            {
                _walking.Add(person);
            }
        }
    }

    /// <summary>
    ///     Opens the filter prompt, which is what <c>f</c> does — and nothing at all on a browsed list, where there
    ///     is no honest filter to offer and the key is not on the row either.
    /// </summary>
    public void Filtering()
    {
        if (Holds)
        {
            _typing = true;
        }
    }

    /// <summary>Hands the screen back to walking with what was typed still narrowing it, which is what <c>⏎</c> does.</summary>
    public void Done() => _typing = false;

    /// <summary>
    ///     Takes the filter off and puts everyone back, which is what <c>esc</c> does before it means back.
    /// </summary>
    /// <returns>
    ///     Whether there was one to take off, which is what settles whether <c>esc</c> was spent on it — the same
    ///     answer a picked reference and an uncast vote give.
    /// </returns>
    public bool Clear()
    {
        if (!_typing && Filter.Length == 0)
        {
            return false;
        }

        _typing = false;
        Filter = string.Empty;
        Narrowed();

        return true;
    }

    /// <inheritdoc />
    /// <remarks>Into the filter, which narrows on the letter rather than on the ⏎ after it.</remarks>
    public override void Type(char letter)
    {
        if (!_typing)
        {
            return;
        }

        Filter += letter;
        Narrowed();
    }

    /// <inheritdoc />
    public override void Backspace()
    {
        if (!_typing)
        {
            return;
        }

        Filter = Backspaced(Filter);
        Narrowed();
    }

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;

        var lines = new List<Line>();

        if (Notice is { } notice)
        {
            lines.Add(Line.Of(TextWrap.Clip(notice, width), Role.Muted));
            lines.Add(Line.Blank);
        }

        if (_typing || Filter.Length > 0)
        {
            lines.Add(Prompt(width));
            lines.Add(Line.Blank);
        }

        // No count over a list nobody has arrived on: "0 of 5 read" is a sum a reader has to work out, and the
        // notice above has already said the whole of what it would have told them.
        if (Read > 0)
        {
            lines.Add(Line.Of(TextWrap.Clip(Counted(), width), Role.Muted));
            lines.Add(Line.Rule(width));
        }

        // No rule between people, and one under the prompt instead: there is one kind of thing on this list and
        // nothing to separate it from, and at 900 people a rule apiece would be 900 rows of horizontal line (#180).
        for (var at = 0; at < _walking.Count; at++)
        {
            lines.AddRange(_walking.RowsOf(at, width, Draw));
        }

        return lines;

        IReadOnlyList<Line> Draw(Account person, int _, int room) => [AccountLines.Person(person, room, Implies)];
    }

    /// <summary>
    ///     How far the reading has got, and how much of it the filter is leaving: both numbers while it is still
    ///     going, so that a thin result reads as still reading rather than as nobody matching (#180).
    /// </summary>
    /// <remarks>
    ///     A browsed list says both numbers always, since what has been read is never the whole of it — which is the
    ///     same row saying why there is no filter on offer.
    /// </remarks>
    private string Counted()
    {
        // Both numbers wherever what is on screen is not the whole of the list — still coming, there to be asked
        // for, or stopped short by a rate limit. Only a list read to the end says the one number, because only then
        // is the one number true.
        if (Read < Total)
        {
            var reading = $"{Number.Of(Read)} of {Number.Of(Total)} read";

            return Filter.Length > 0 ? $"{Number.Of(_walking.Count)} matching · {reading}" : reading;
        }

        return Filter.Length > 0
            ? $"{Number.Of(_walking.Count)} of {Number.Of(Total)} {Called}"
            : $"{Number.Of(Total)} {Called}";
    }

    /// <summary>
    ///     What is narrowing the list, with the caret where the next letter lands — the search prompt's own
    ///     construction, so that the one thing this shell does with typed text looks like itself everywhere.
    /// </summary>
    private Line Prompt(int width)
    {
        const string label = "Filter: ";
        var typed = TextWrap.Clip(Filter, Math.Max(0, width - label.Length - 1));

        return _typing
            ? Line.Of([new Span(label, Role.Muted), new Span(typed, Role.Body), new Span("▌", Role.Selection)])
            : Line.Of([new Span(label, Role.Muted), new Span(typed, Role.Body)]);
    }

    /// <summary>Whether <paramref name="person" /> is one the filter leaves on screen, by either of the names they go by.</summary>
    /// <remarks>
    ///     Their handle as well as their display name, since a reader half-remembering somebody may have either in
    ///     mind — and neither is what the other is: a display name is not unique and a handle is not what anybody
    ///     calls them.
    /// </remarks>
    private bool Matches(Account person) =>
        Filter.Length == 0
        || person.Author.Contains(Filter, StringComparison.CurrentCultureIgnoreCase)
        || person.Address.Contains(Filter, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    ///     Puts whoever the filter now leaves on screen, with the pick back at the top of them: a narrowing that kept
    ///     its index would be standing on somebody the reader has just filtered away.
    /// </summary>
    private void Narrowed() => _walking = new Picked<Account>([.. _all.Where(Matches)]);
}
