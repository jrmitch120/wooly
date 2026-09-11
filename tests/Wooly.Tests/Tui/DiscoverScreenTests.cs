using Wooly.Core.Accounts;
using Wooly.Core.Discovery;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     The Discover screen: who the instance is offering, grouped under the reason it offered them, with the two keys
///     that act on a row and the one rule they both obey — nothing moves (#181, ADR-0019).
/// </summary>
/// <remarks>
///     Drawn with no terminal and no instance, the way every other screen in this shell is tested: the screen holds
///     what it was handed and says what it draws, and what a key means to an instance is the shell's (ADR-0005).
/// </remarks>
public class DiscoverScreenTests
{
    private static readonly Drawing At61 = new(61, AShell.Now);

    /// <summary>The five headings, in the fixed rank the sections are drawn in.</summary>
    public static TheoryData<SuggestionReason, string> EveryReason => new()
    {
        { SuggestionReason.FriendsOfFriends, "── 1 followed by people you follow ──" },
        { SuggestionReason.SimilarToRecentlyFollowed, "── 1 like people you followed lately ──" },
        { SuggestionReason.Featured, "── 1 featured by this instance ──" },
        { SuggestionReason.MostInteractions, "── 1 talked with most here ──" },
        { SuggestionReason.MostFollowed, "── 1 most followed here ──" },
    };

    /// <summary>
    ///     The sections come out in the fixed rank whatever order the instance sent them in, since the rank is what
    ///     says which reason is worth reading and the instance ranked people rather than reasons.
    /// </summary>
    [Fact]
    public void Lines_DrawTheSectionsInTheFixedRankWhateverOrderTheyArrivedIn()
    {
        var screen = Discovering(
            Offered("zoe", SuggestionReason.MostFollowed),
            Offered("yan", SuggestionReason.Featured),
            Offered("xan", SuggestionReason.FriendsOfFriends));

        Assert.Equal(
            [
                "── 1 followed by people you follow ──",
                "── 1 featured by this instance ──",
                "── 1 most followed here ──",
            ],
            Headings(screen));
    }

    /// <summary>Each of the five is spelled the one way <see cref="SuggestionReasonName" /> spells it.</summary>
    [Theory]
    [MemberData(nameof(EveryReason))]
    public void Lines_HeadEverySectionWithTheReasonItIsFor(SuggestionReason reason, string heading) =>
        Assert.Equal([heading], Headings(Discovering(Offered("alice", reason))));

    /// <summary>A heading counts what is under it, which is what says whether the run is worth walking.</summary>
    [Fact]
    public void Lines_CountTheSectionInItsHeading()
    {
        var screen = Discovering(
            Offered("alice", SuggestionReason.FriendsOfFriends),
            Offered("ben", SuggestionReason.FriendsOfFriends),
            Offered("cass", SuggestionReason.MostFollowed));

        Assert.Equal(["── 2 followed by people you follow ──", "── 1 most followed here ──"], Headings(screen));
    }

    /// <summary>A reason nobody was offered under draws no heading, rather than a heading over nothing.</summary>
    [Fact]
    public void Lines_DrawNoHeadingForASectionWithNobodyInIt() =>
        Assert.Equal(
            ["── 1 featured by this instance ──"],
            Headings(Discovering(Offered("alice", SuggestionReason.Featured))));

    /// <summary>
    ///     Somebody offered under several reasons is drawn once, under the best of them: the same face twice would
    ///     carry a second <c>F</c> that does nothing.
    /// </summary>
    [Fact]
    public void People_AreDrawnOnceUnderTheirHighestRankedReason()
    {
        var screen = Discovering(
            ASuggestion.With(Person("alice"), [SuggestionReason.MostFollowed, SuggestionReason.FriendsOfFriends]));

        Assert.Equal(["── 1 followed by people you follow ──"], Headings(screen));
        Assert.Single(screen.People);
    }

    /// <summary>
    ///     The instance's own order inside a section is kept: it ranked them, and re-sorting would assert a judgement
    ///     this client has no data for.
    /// </summary>
    [Fact]
    public void People_KeepTheInstancesOwnOrderInsideASection()
    {
        var screen = Discovering(
            Offered("zoe", SuggestionReason.FriendsOfFriends),
            Offered("alice", SuggestionReason.FriendsOfFriends),
            Offered("mel", SuggestionReason.FriendsOfFriends));

        Assert.Equal(
            ["zoe@hachyderm.io", "alice@hachyderm.io", "mel@hachyderm.io"],
            screen.People.Select(person => person.Address));
    }

    /// <summary>
    ///     Somebody offered under a source this client has no heading for is still somebody worth showing, so they
    ///     stand above the first heading as a run of their own rather than being dropped or drawn under a reason that
    ///     is not theirs.
    /// </summary>
    [Fact]
    public void People_OfferedUnderNoReasonThisClientNamesStandAboveTheFirstHeading()
    {
        var screen = Discovering(
            ASuggestion.With(Person("nobody"), []),
            Offered("alice", SuggestionReason.FriendsOfFriends));

        var lines = screen.Lines(At61).ToList();

        var unheaded = lines.FindIndex(line => line.Text.Contains("nobody@hachyderm.io", StringComparison.Ordinal));
        var heading = lines.FindIndex(line => line.Heads);

        Assert.True(unheaded >= 0);
        Assert.True(unheaded < heading);
    }

    /// <summary>
    ///     A row is the Account block every listing draws and nothing else: the heading above has already said why
    ///     they are there, and somebody found here reads the way they read on the screen they are opened from (#198).
    /// </summary>
    [Fact]
    public void Lines_DrawARowAsTheSharedAccountBlock()
    {
        var screen = Discovering(Offered("alice", SuggestionReason.FriendsOfFriends));

        // Past the one column the gutter takes, which every list on this shell stamps and no screen draws itself.
        Assert.Equal(
            ["Alice", "@alice@hachyderm.io", "4,210 posts · 187 following · 1,203 followers", "Joined Jan 2020"],
            Row(screen, "alice@hachyderm.io").Select(row => row[1..]));
    }

    /// <summary>
    ///     A blank stands between two people, since four-row blocks laid end to end run together — and none where a
    ///     heading is already standing between them, that bringing its own.
    /// </summary>
    [Fact]
    public void Lines_PutOneBlankBetweenPeopleInTheSameSection()
    {
        var screen = Discovering(
            Offered("alice", SuggestionReason.FriendsOfFriends),
            Offered("ben", SuggestionReason.FriendsOfFriends));

        var rows = screen.Lines(At61).Select(line => line.Text).ToList();

        var alice = rows.FindIndex(row => row.Contains("@alice@hachyderm.io", StringComparison.Ordinal));
        var ben = rows.FindIndex(row => row.Contains("@ben@hachyderm.io", StringComparison.Ordinal));

        Assert.Equal(5, ben - alice);
        Assert.Equal(string.Empty, rows[alice + 3].Trim());
    }

    /// <summary>
    ///     <c>F</c> appends a muted suffix and takes it off again, the same contract it has on the account screen.
    /// </summary>
    [Fact]
    public void Following_AppendsTheSuffixAndTakesItOffAgain()
    {
        var screen = Discovering(Offered("alice", SuggestionReason.FriendsOfFriends));

        screen.Stands(Following("alice"));

        Assert.Contains(" · following", Facts(screen, "alice@hachyderm.io"));

        screen.Stands(Person("alice") with { Standing = AnAccount.Standing() });

        Assert.DoesNotContain(" · following", Facts(screen, "alice@hachyderm.io"));
    }

    /// <summary>A follow that only got as far as a request says so, the way the account screen's own does.</summary>
    [Fact]
    public void Following_SaysAskedWhereTheFollowIsStillWaiting()
    {
        var screen = Discovering(Offered("alice", SuggestionReason.FriendsOfFriends));

        screen.Stands(Person("alice") with { Standing = AnAccount.Standing(followRequested: true) });

        Assert.Contains(" · asked", Facts(screen, "alice@hachyderm.io"));
    }

    /// <summary><c>d</c> appends its own suffix, and both suffixes stand together where both are true.</summary>
    [Fact]
    public void Dismissing_AppendsTheSuffixAlongsideAFollow()
    {
        var screen = Discovering(Offered("alice", SuggestionReason.FriendsOfFriends));

        screen.Stands(Following("alice"));
        screen.Dismissed("alice");

        Assert.Contains(" · following · dismissed", Facts(screen, "alice@hachyderm.io"));
    }

    /// <summary>
    ///     Nothing moves under either key: no row vanishes, no row reorders, and no heading recounts. The feature was
    ///     admitted for <c>j j j F F</c>, and a list that reflows under the reader's fingers is what breaks it.
    /// </summary>
    [Fact]
    public void Acting_MovesNothingAtAll()
    {
        var screen = Discovering(
            Offered("alice", SuggestionReason.FriendsOfFriends),
            Offered("ben", SuggestionReason.FriendsOfFriends),
            Offered("cass", SuggestionReason.MostFollowed));

        var before = screen.People.Select(person => person.Address).ToList();
        var headings = Headings(screen);

        screen.Move(1);

        var picked = screen.PickedPerson!.Address;

        screen.Stands(Following("ben"));
        screen.Dismissed("ben");

        Assert.Equal(before, screen.People.Select(person => person.Address));
        Assert.Equal(headings, Headings(screen));
        Assert.Equal(picked, screen.PickedPerson!.Address);
    }

    /// <summary>
    ///     A dismissed row stays walkable and still answers <c>⏎</c> and <c>F</c>: dismissing says "stop suggesting",
    ///     not "hide".
    /// </summary>
    [Fact]
    public void Dismissed_RowsStayOnTheListAndStayPicked()
    {
        var screen = Discovering(Offered("alice", SuggestionReason.FriendsOfFriends));

        screen.Dismissed("alice");

        Assert.NotNull(screen.PickedPerson);
        Assert.True(screen.IsDismissed(screen.PickedPerson!.Id));
        Assert.Single(screen.People);
    }

    /// <summary>Asked by the same id the dismissal names a row by, so a second <c>d</c> has something to be a no-op by.</summary>
    [Fact]
    public void IsDismissed_SaysWhetherARowHasAlreadyBeenDismissed()
    {
        var screen = Discovering(Offered("alice", SuggestionReason.FriendsOfFriends));

        var person = screen.PickedPerson!;

        Assert.False(screen.IsDismissed(person.Id));

        screen.Dismissed(person.Id);

        Assert.True(screen.IsDismissed(person.Id));
    }

    /// <summary>Both suffixes are muted, so nothing on this screen is colour.</summary>
    [Fact]
    public void Lines_DrawBothSuffixesMuted()
    {
        var screen = Discovering(Offered("alice", SuggestionReason.FriendsOfFriends));

        screen.Stands(Following("alice"));
        screen.Dismissed("alice");

        var row = screen.Lines(At61).Single(line => line.Text.EndsWith("following · dismissed", StringComparison.Ordinal));

        Assert.All(
            row.Spans.Where(span => span.Text.Contains('·')),
            span => Assert.Equal(Role.Muted, span.Role));
    }

    /// <summary>
    ///     An instance offering nobody is a real answer and is said as one, with no apology for a new account or a
    ///     small instance — the notice an arrival hands down, on the construction search's own empty answer uses.
    /// </summary>
    [Fact]
    public void Lines_SayWhatAnArrivalHadToSayAboutAnEmptyList()
    {
        var screen = new DiscoverScreen([], "Nobody suggested.");

        Assert.Equal("Nobody suggested.", screen.Lines(At61)[0].Text);
    }

    /// <summary>And nothing at all while the arrival has not answered yet, which is the empty screen it puts up.</summary>
    [Fact]
    public void Lines_SayNothingAtAllWhileNothingHasBeenSaid() => Assert.Empty(new DiscoverScreen([]).Lines(At61));

    /// <summary>The crumb, which is the whole of what the breadcrumb says on an arrival.</summary>
    [Fact]
    public void Crumb_IsTheDestinationsOwnName() => Assert.Equal("Discover", new DiscoverScreen([]).Crumb);

    /// <summary>
    ///     The status row puts the acting keys first, which is the opposite of the search screen's placement and
    ///     deliberately: <c>F</c> and <c>d</c> are why anyone opened this screen.
    /// </summary>
    [Fact]
    public void Keys_PutTheActingKeysFirst() =>
        Assert.Equal(
            "j/k:person ⏎:open F:follow d:dismiss g:refresh ↓/↑:row tab:destination ?:keys",
            string.Join(' ', Discovering(Offered("alice", SuggestionReason.FriendsOfFriends)).Keys));

    /// <summary>
    ///     And <c>[/]</c> only where there are two sections to move between, since a key announced where it does
    ///     nothing reads as a shell that missed the press.
    /// </summary>
    [Fact]
    public void Keys_SayTheSectionKeyOnlyWhereThereIsSomewhereToJump()
    {
        var one = Discovering(Offered("alice", SuggestionReason.FriendsOfFriends));
        var two = Discovering(
            Offered("alice", SuggestionReason.FriendsOfFriends),
            Offered("ben", SuggestionReason.MostFollowed));

        Assert.DoesNotContain("[/]:section", one.Keys.Select(key => key.ToString()));
        Assert.Contains("[/]:section", two.Keys.Select(key => key.ToString()));
    }

    /// <summary><c>F</c> says which way it goes, the way the account screen's own tie keys do.</summary>
    [Fact]
    public void Keys_SayWhichWayFollowGoes()
    {
        var screen = Discovering(Offered("alice", SuggestionReason.FriendsOfFriends));

        Assert.Contains("F:follow", screen.Keys.Select(key => key.ToString()));

        screen.Stands(Following("alice"));

        Assert.Contains("F:unfollow", screen.Keys.Select(key => key.ToString()));
    }

    /// <summary>The rows are marked as headed runs, which is what <c>[</c> and <c>]</c> move between (#177).</summary>
    [Fact]
    public void Lines_MarkEveryHeadingAsOneSoTheSectionKeyCanFindIt()
    {
        var screen = Discovering(
            Offered("alice", SuggestionReason.FriendsOfFriends),
            Offered("ben", SuggestionReason.MostFollowed));

        var lines = screen.Lines(At61);

        Assert.Equal(2, lines.Count(line => line.Heads));
        Assert.NotNull(Sections.Along(lines, 1, reclaiming: null));
    }

    /// <summary>
    ///     A suggestion of <paramref name="address" />, offered for <paramref name="reason" /> — named by an id of
    ///     their own, since both keys on this screen name a row by one.
    /// </summary>
    private static Suggestion Offered(string address, SuggestionReason reason) =>
        ASuggestion.With(Person(address), [reason]);

    /// <summary>
    ///     Them, named by an id of their own — since both keys on this screen name a row by one, and a test where
    ///     everybody shares an id proves nothing about either.
    /// </summary>
    private static Account Person(string address) => AnAccount.With(
        address: $"{address}@hachyderm.io",
        author: char.ToUpperInvariant(address[0]) + address[1..],
        id: address);

    /// <summary>A screen holding what an instance offered, which is what an arrival hands it.</summary>
    private static DiscoverScreen Discovering(params Suggestion[] suggestions) => new(suggestions);

    /// <summary>Them, as the instance answers once a follow has gone through.</summary>
    private static Account Following(string address) =>
        Person(address) with { Standing = AnAccount.Standing(following: true) };

    /// <summary>The headings the screen drew, in the order it drew them.</summary>
    private static IReadOnlyList<string> Headings(DiscoverScreen screen) =>
        [.. screen.Lines(At61).Where(line => line.Heads).Select(line => line.Text.Trim())];

    /// <summary>The one row <paramref name="address" /> is drawn on.</summary>
    private static IReadOnlyList<string> Row(DiscoverScreen screen, string address)
    {
        var lines = screen.Lines(At61);

        // Found by the handle, which is the one row of a block carrying an address — and the name is the row above it.
        var handle = lines
            .Select((line, at) => (line.Text, At: at))
            .Single(found => found.Text.Contains($"@{address}", StringComparison.Ordinal))
            .At;

        return [.. lines.Skip(handle - 1).Take(4).Select(line => line.Text)];
    }

    /// <summary>Row 4 of that block: the month they joined, their flags, and what the reader has done here.</summary>
    private static string Facts(DiscoverScreen screen, string address) => Row(screen, address)[3];
}
