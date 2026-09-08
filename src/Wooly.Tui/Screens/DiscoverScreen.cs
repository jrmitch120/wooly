using Wooly.Core.Accounts;
using Wooly.Core.Discovery;
using Wooly.Core.Relationships;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     Who the instance offers this profile to follow, in sections by why it is offering them — the tenth rail
///     destination, and the first the rail has ever grown (<c>docs/tui-shell.md</c>, ADR-0019, #181).
/// </summary>
/// <remarks>
///     A list of strangers is a list of strangers, so what earns a stranger a row is the heading over it. Every
///     reason gets one, weak ones included: a heading is what makes "most followed here" readable <i>as</i> weak
///     rather than an unexplained row, and the reader discounts it themselves.
///     <para>
///         A row is a plain <see cref="AccountLines.Byline" /> and no relationships call — a deliberate departure from
///         the follow list's recipe. Mastodon's suggestion sources exclude accounts already followed, dismissed or
///         blocked, so a standing suffix would be blank on every row by construction; this screen is one call, not two
///         (ADR-0019). What the suffixes say is what the reader has done here, since arriving.
///     </para>
///     <para>
///         <strong>Nothing moves.</strong> No row vanishes, no row reorders and no heading recounts as the reader
///         acts: the grouping is settled once, when the answer arrives, and both keys only ever rewrite a row where it
///         stands. The feature was admitted for <c>j j j F F</c>, and a list that reflows under somebody's fingers is
///         the one thing that breaks it.
///     </para>
///     <para>
///         Nothing here fetches. What is read is the shell's, so the whole of this screen can be walked, acted on and
///         drawn with no terminal and no instance.
///     </para>
/// </remarks>
public sealed class DiscoverScreen : Screen
{
    /// <summary>What each suffix is joined on by, which is also what puts one after the byline.</summary>
    private const string Joined = " · ";

    /// <summary>What <c>d</c> appends, which never comes off again: there is no un-dismiss endpoint.</summary>
    private const string DismissedSaid = "dismissed";

    /// <summary>
    ///     Everyone being offered, grouped and in the order they are drawn and walked — which is the same order by
    ///     construction, this being the one list the screen holds.
    /// </summary>
    private readonly Picked<Offer> _offered;

    /// <param name="suggested">
    ///     Who the instance offered, in its own order — which is its ranking, and is kept inside every section.
    /// </param>
    /// <param name="notice">
    ///     What the arrival has to say about the read rather than about anybody on it: that the instance offered
    ///     nobody, or that a rate limit cut it short. Nothing at all while nothing has been asked yet, which is the
    ///     empty screen an arrival puts up before its answer lands.
    /// </param>
    public DiscoverScreen(IReadOnlyList<Suggestion> suggested, string? notice = null)
    {
        _offered = new Picked<Offer>(Grouped(suggested));
        Notice = notice;
    }

    /// <inheritdoc />
    public override string Crumb => "Discover";

    /// <inheritdoc />
    public override bool Refreshes => true;

    /// <summary>Everyone on the list, in the order they are drawn.</summary>
    public IReadOnlyList<Account> People => [.. _offered.All.Select(offer => offer.Person)];

    /// <summary>
    ///     What the shell has to say about the list rather than about anybody on it. An instance offering nobody is a
    ///     real answer and is said as one — no apology for a new account or a small instance, and no
    ///     empty-versus-not-asked to draw, since a destination always asks on arrival.
    /// </summary>
    public string? Notice { get; }

    /// <summary>Whoever is picked out, or nobody where nobody is on screen. What <c>⏎</c>, <c>F</c> and <c>d</c> act on.</summary>
    public Account? PickedPerson => _offered.Out?.Person;

    /// <inheritdoc />
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        // The acting keys first, which is the opposite of the search screen's placement and deliberately: there,
        // jumping between kinds is the feature that screen gained; here, F and d are why anyone opened this screen.
        new KeyHint("j/k", "person"),
        new KeyHint("⏎", "open", NeedsAPick: true),
        new KeyHint("F", Follows ? "unfollow" : "follow", NeedsAPick: true),
        new KeyHint("d", "dismiss", NeedsAPick: true),
        .. Jumping,
        Refreshing,
        PostKeys.Scrolling,
        new KeyHint("tab", "destination"),
        new KeyHint("?", "keys"),
    ];

    /// <inheritdoc />
    protected override IPicked Walking => _offered;

    /// <summary>
    ///     The key that moves between this screen's sections, said only where there are two to move between — the same
    ///     rule and the same words the search screen states, because one key that means one thing shell-wide is named
    ///     one way shell-wide (#166, #177).
    /// </summary>
    /// <remarks>
    ///     Counted off <see cref="Heads" />, which is the very predicate <see cref="Heading" /> draws by — so the runs
    ///     the reader can see are the runs the key is announced for, with no second count kept anywhere. That is the
    ///     arrangement the search screen states and CONTEXT.md's <b>Section</b> requires.
    ///     <para>
    ///         The run standing above the first heading counts as one of them, since <c>]</c> reaches the first headed
    ///         run from there: a key that could move but was not announced is as wrong as one announced where it
    ///         cannot.
    ///     </para>
    /// </remarks>
    private IReadOnlyList<KeyHint> Jumping =>
        Enumerable.Range(0, _offered.Count).Count(Heads) + (Unheaded ? 1 : 0) > 1
            ? [new KeyHint("[/]", "section")]
            : [];

    /// <summary>
    ///     Whether anybody stands above the first heading, which is a run of its own — the position the account
    ///     screen's header block occupies, and what <c>[</c> clamps at.
    /// </summary>
    private bool Unheaded => _offered.Count > 0 && _offered.All[0].Under is null;

    /// <summary>
    ///     Whether the picked person is already followed, which is what <c>F</c> offers to undo — a follow still
    ///     waiting to be let in counted the same way the account screen counts it.
    /// </summary>
    private bool Follows => PickedPerson?.Standing?.Has(AccountTie.Follow) ?? false;

    /// <inheritdoc />
    /// <remarks>
    ///     The row is rewritten where it stands. Nothing is added, taken away or reordered, so a reader part way
    ///     through <c>j j j F F</c> is still standing where they were — which is the one rule this screen exists to
    ///     keep.
    /// </remarks>
    public override void Stands(Account account) =>
        _offered.Rewrite(offer => offer.Person.Id == account.Id ? offer with { Person = account } : offer);

    /// <summary>
    ///     Marks the row <paramref name="accountId" /> names as one the instance has been told to stop suggesting.
    /// </summary>
    /// <remarks>
    ///     The row stays: dismissing says "stop suggesting", not "hide", and a row that vanished would take its own
    ///     undo with it — which matters here more than anywhere, there being no un-dismiss endpoint to undo it with.
    ///     It stays walkable and goes on answering <c>⏎</c> and <c>F</c>.
    /// </remarks>
    public void Dismissed(string accountId) =>
        _offered.Rewrite(offer => offer.Person.Id == accountId ? offer with { Gone = true } : offer);

    /// <summary>
    ///     Whether the instance has already been told to stop suggesting <paramref name="accountId" />, which is what
    ///     makes a second <c>d</c> a no-op rather than a second call saying what the first one said.
    /// </summary>
    /// <remarks>By the same id <see cref="Dismissed" /> names a row by, so the question and the answer agree.</remarks>
    public bool IsDismissed(string accountId) =>
        _offered.All.Any(offer => offer.Person.Id == accountId && offer.Gone);

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

        // No rule between people, and the headings to separate the runs instead: there is one kind of thing on this
        // list and every one of them is a single row, so a rule apiece would be forty rows of horizontal line — the
        // same judgement the follow list made (#180).
        for (var at = 0; at < _offered.Count; at++)
        {
            lines.AddRange(Heading(at, width));
            lines.AddRange(_offered.RowsOf(at, width, Draw));
        }

        return lines;

        IReadOnlyList<Line> Draw(Offer offer, int _, int room) => [Row(offer, room)];
    }

    /// <summary>
    ///     Everyone offered, each under the best reason they were offered for, with the sections in the fixed rank —
    ///     which is <see cref="SuggestionReason" />'s own declaration order rather than a second list kept in step
    ///     with it.
    /// </summary>
    /// <remarks>
    ///     Done once, when the answer arrives, because it is what makes the list hold still afterwards: a screen that
    ///     regrouped as the reader acted is a screen that reflows under their fingers.
    ///     <para>
    ///         A person is drawn once — <c>sources</c> is an array and the same face twice would carry a second
    ///         <c>F</c> that does nothing — and merged across the whole answer rather than within one suggestion, so
    ///         somebody an instance named twice is still one row under the better of the two reasons.
    ///     </para>
    ///     <para>
    ///         Somebody offered under a source this client has no heading for stands <em>above</em> the first heading,
    ///         as a run of their own — the position the account screen's header block already occupies, so <c>]</c>
    ///         reaches the first headed run from there and <c>[</c> clamps. Dropped would be the alternative, and it
    ///         is the wrong one twice over: an instance that has learned a sixth source has still named somebody worth
    ///         showing (CONTEXT.md), and a heading invented for a word this client cannot spell would be the one
    ///         dishonest row on the screen. Below the last section they could not go — they would read as part of it,
    ///         and its heading counts only its own.
    ///     </para>
    /// </remarks>
    private static IReadOnlyList<Offer> Grouped(IReadOnlyList<Suggestion> suggested)
    {
        var best = new Dictionary<string, SuggestionReason?>(StringComparer.Ordinal);

        // First-seen order, which is the instance's own ranking and is what is kept inside every section.
        var people = new List<Account>();

        foreach (var suggestion in suggested)
        {
            var reason = Best(suggestion);

            if (best.TryGetValue(suggestion.Account.Id, out var held))
            {
                best[suggestion.Account.Id] = Ranked(reason) < Ranked(held) ? reason : held;
            }
            else
            {
                best[suggestion.Account.Id] = reason;
                people.Add(suggestion.Account);
            }
        }

        var offers = new List<Offer>(people.Count);

        offers.AddRange(people.Where(person => best[person.Id] is null).Select(person => new Offer(person, null)));

        foreach (var reason in Enum.GetValues<SuggestionReason>())
        {
            offers.AddRange(people
                .Where(person => best[person.Id] == reason)
                .Select(person => new Offer(person, reason)));
        }

        return offers;
    }

    /// <summary>
    ///     The best of the reasons one suggestion carries, or <see langword="null" /> where it names none this client
    ///     has a heading for — the lowest member, the enum being declared in the rank a screen draws its sections in.
    /// </summary>
    private static SuggestionReason? Best(Suggestion suggestion) =>
        suggestion.Reasons.Count == 0 ? null : suggestion.Reasons.Min();

    /// <summary>
    ///     Where a reason stands in the rank, for choosing between two the same person was offered under. No reason at
    ///     all ranks below every named one, so somebody an instance named twice — once under a source this client
    ///     spells and once under one it does not — is drawn under the one it spells.
    /// </summary>
    /// <remarks>
    ///     About which reason wins, not about where the section is drawn: the run of people with no reason at all is
    ///     drawn <em>first</em>, which <see cref="Grouped" /> settles.
    /// </remarks>
    private static int Ranked(SuggestionReason? reason) => reason is { } named ? (int)named : int.MaxValue;

    /// <summary>
    ///     One person as a row: their byline, and what the reader has done to them since arriving. Nothing else — the
    ///     heading above has already said why they are there, and a row never says what the list it is on already
    ///     says.
    /// </summary>
    private static Line Row(Offer offer, int width) => AccountLines.Beside(offer.Person, width, Suffix(offer));

    /// <summary>
    ///     What the two keys have left on this row — a follow and a dismissal being different things and both able to
    ///     be true, so both are said rather than one standing in for the other.
    /// </summary>
    /// <remarks>
    ///     The follow is worded by <see cref="AccountLines.Follow" /> rather than here, so that a follow reads the
    ///     same way on this screen as on a follow list — including the one that is still waiting for a locked account
    ///     to accept it, which is not a follow yet and must not be drawn as one (CONTEXT.md).
    /// </remarks>
    private static string Suffix(Offer offer)
    {
        var said = new List<string>(2);

        if (AccountLines.Follow(offer.Person.Standing) is { Length: > 0 } follow)
        {
            said.Add(follow);
        }

        if (offer.Gone)
        {
            said.Add(DismissedSaid);
        }

        return said.Count == 0 ? string.Empty : Joined + string.Join(Joined, said);
    }

    /// <summary>
    ///     The heading over the person at <paramref name="at" />, where they are the first of their section — so a
    ///     reason nobody was offered under gets no heading rather than a heading over nothing.
    /// </summary>
    /// <remarks>
    ///     Marked as a heading rather than only drawn like one, which is what <c>[</c> and <c>]</c> move between: the
    ///     runs the reader can see are then the runs the key knows about, with no second count of them kept anywhere
    ///     (<see cref="Sections" />).
    ///     <para>
    ///         The count is the whole of the section, which is what tells a reader whether the run below is worth
    ///         walking or worth jumping past — and it cannot drift, being counted off the same list the rows are drawn
    ///         from every frame.
    ///     </para>
    /// </remarks>
    private IReadOnlyList<Line> Heading(int at, int width)
    {
        if (!Heads(at) || _offered.All[at].Under is not { } reason)
        {
            return [];
        }

        var found = _offered.All.Count(one => one.Under == reason);

        var heading = Line.Of(
            TextWrap.Clip($"── {found} {SuggestionReasonName.Of(reason)} ──", width),
            Role.Muted).Heading();

        // A blank above every heading but the first, which is what separates one section from the last row of the one
        // before it — the job the search screen's rule between results happens to do there.
        return at > 0 ? [Line.Blank, heading, Line.Blank] : [heading, Line.Blank];
    }

    /// <summary>
    ///     Whether the person at <paramref name="at" /> is the first of a headed run, and so the one a heading stands
    ///     over — which is the whole of where a run begins on this screen.
    /// </summary>
    /// <remarks>
    ///     Said here rather than twice, because the runs <see cref="Jumping" /> counts have to be the runs
    ///     <see cref="Heading" /> draws: a status row announcing a key against a second count of its own is how the
    ///     two come to disagree (<see cref="Sections" />, #166).
    /// </remarks>
    private bool Heads(int at) =>
        _offered.All[at].Under is not null && (at == 0 || _offered.All[at - 1].Under != _offered.All[at].Under);

    /// <summary>
    ///     One person on the list: who they are as the instance last had them, which reason they are drawn under, and
    ///     whether the instance has been told to stop suggesting them.
    /// </summary>
    /// <remarks>
    ///     A record rather than the bare <see cref="Suggestion" /> the port hands over, because two of the three
    ///     change after the answer arrives and the third settles where the row is drawn: <see cref="Person" /> is
    ///     replaced by what the instance answers a follow with, and <see cref="Gone" /> is what <c>d</c> leaves
    ///     behind. Keeping the reason on the row rather than in a list of sections is what makes the heading's count
    ///     and the rows under it one fact.
    /// </remarks>
    /// <param name="Person">Them, carrying whatever standing the instance has since answered with.</param>
    /// <param name="Under">
    ///     The reason they are drawn under, or <see langword="null" /> where the instance named none this client has a
    ///     heading for — which is the run standing above the first heading.
    /// </param>
    /// <param name="Gone">Whether the instance has been told to stop suggesting them, which is one-way.</param>
    private sealed record Offer(Account Person, SuggestionReason? Under, bool Gone = false);
}
