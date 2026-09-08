using Wooly.Core.Accounts;
using Wooly.Core.Discovery;
using Wooly.Core.Errors;
using Wooly.Core.Relationships;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     Arriving at Discover and acting on what is there: the tenth destination, the one call it costs, and the two
///     keys that reach an instance from it (#181, ADR-0019).
/// </summary>
/// <remarks>
///     At the port seam, with no terminal — what a key asks an instance for is a fact about the shell, and what the
///     row then says is the screen's and is asserted in <see cref="DiscoverScreenTests" />.
/// </remarks>
public class ShellDiscoverTests
{
    /// <summary>Arriving asks the instance once, for the forty a screen with no paging shows all of.</summary>
    [Fact]
    public async Task Arriving_AsksOnceForFortySuggestions()
    {
        var (fakes, opened) = await OnDiscover();

        Assert.IsType<DiscoverScreen>(opened.Screen);

        var asked = Assert.Single(fakes.Suggestions.Reads);
        Assert.Equal(40, asked.Limit);
        Assert.Equal("personal", asked.Profile);
    }

    /// <summary>And puts the stack back to one screen, Discover being an arrival rather than something drilled into.</summary>
    [Fact]
    public async Task Arriving_ResetsTheStackToOneScreen()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        await opened.Enter();
        shell.Host.Drain();

        Assert.Equal(2, opened.Depth);

        opened.Rail.GoTo(DestinationKind.Discover);
        shell.Host.Drain();

        Assert.Equal(1, opened.Depth);
        Assert.Equal("Discover", opened.Breadcrumb);
    }

    /// <summary>Nothing here is waiting for anybody, so the rail carries no count for it — the same as Search.</summary>
    [Fact]
    public async Task Arriving_LeavesTheBadgeAtZero()
    {
        var (_, opened) = await OnDiscover(
            ASuggestion.With(AnAccount.With(address: "alice@hachyderm.io", id: "1")),
            ASuggestion.With(AnAccount.With(address: "ben@hachyderm.io", id: "2")));

        Assert.Equal(0, Discover(opened).Unread);
    }

    /// <summary>An instance offering nobody is a real answer, and is said as one rather than drawn as a blank screen.</summary>
    [Fact]
    public async Task Arriving_SaysNobodyWasSuggestedWhereTheInstanceOffersNobody()
    {
        var shell = new AShell { Suggestions = FakeFollowSuggestions.OfferingNobody() };
        var opened = await shell.Opened();

        opened.Rail.GoTo(DestinationKind.Discover);
        shell.Host.Drain();

        var screen = Assert.IsType<DiscoverScreen>(opened.Screen);
        Assert.Equal("Nobody suggested.", screen.Notice);
        Assert.Null(opened.Notice);
    }

    /// <summary>
    ///     A failure is not an empty screen: the enquiry turns it into the shell's own notice, and the screen goes on
    ///     saying nothing about a list it never got.
    /// </summary>
    [Fact]
    public async Task Arriving_TurnsARefusalIntoTheShellsNoticeRatherThanAnEmptyScreen()
    {
        var shell = new AShell
        {
            Suggestions = FakeFollowSuggestions.Refusing(new AuthenticationException("No.")),
        };

        var opened = await shell.Opened();

        opened.Rail.GoTo(DestinationKind.Discover);
        shell.Host.Drain();

        var screen = Assert.IsType<DiscoverScreen>(opened.Screen);
        Assert.Null(screen.Notice);
        Assert.Equal("No.", opened.Notice);
        Assert.True(opened.NoticeIsError);
    }

    /// <summary><c>F</c> follows whoever is picked out, which is the same call the account screen's own key makes.</summary>
    [Fact]
    public async Task Follow_PutsTheTieOnWhoeverIsPickedOut()
    {
        var (fakes, opened) = await OnDiscover();

        await opened.Tie(AccountTie.Follow);
        fakes.Host.Drain();

        var tied = Assert.Single(fakes.Accounts.Ties);
        Assert.Equal("alice@hachyderm.io", tied.Account.Text);
        Assert.Equal(AccountTie.Follow, tied.Tie);
        Assert.True(tied.Wanted);
    }

    /// <summary>And takes it off again, the toggle being the whole of what <c>F</c> means here.</summary>
    [Fact]
    public async Task Follow_TakesTheTieOffAgainOnASecondPress()
    {
        var followed = AnAccount.With(
            address: "alice@hachyderm.io",
            id: "1",
            standing: AnAccount.Standing(following: true));

        var (fakes, opened) = await OnDiscover();

        fakes.Accounts.Becoming = followed;

        await opened.Tie(AccountTie.Follow);
        fakes.Host.Drain();

        await opened.Tie(AccountTie.Follow);
        fakes.Host.Drain();

        Assert.Equal([true, false], fakes.Accounts.Ties.Select(tied => tied.Wanted));
    }

    /// <summary>
    ///     The account the instance answered with replaces the copy the row is holding, so the suffix appears where
    ///     the key was pressed rather than the next time the screen is opened.
    /// </summary>
    [Fact]
    public async Task Follow_DrawsTheStandingAsTheInstanceNowHasIt()
    {
        var (fakes, opened) = await OnDiscover();

        fakes.Accounts.Becoming = AnAccount.With(
            address: "alice@hachyderm.io",
            id: "1",
            standing: AnAccount.Standing(following: true));

        await opened.Tie(AccountTie.Follow);
        fakes.Host.Drain();

        var screen = Assert.IsType<DiscoverScreen>(opened.Screen);
        Assert.True(screen.PickedPerson?.Standing?.Following);
    }

    /// <summary>
    ///     Only <c>F</c>: the account screen is where a reader has the whole of somebody in front of them, which is
    ///     what <c>M</c> and <c>B</c> are worth pressing against — and this screen announces neither.
    /// </summary>
    [Theory]
    [InlineData(AccountTie.Mute)]
    [InlineData(AccountTie.Block)]
    public async Task Tie_DoesNothingButFollowOnDiscover(AccountTie tie)
    {
        var (fakes, opened) = await OnDiscover();

        await opened.Tie(tie);
        fakes.Host.Drain();

        Assert.Empty(fakes.Accounts.Ties);
        Assert.DoesNotContain(opened.Keys, key => key.Key is "M" or "B");
    }

    /// <summary><c>d</c> tells the instance to stop suggesting the person picked out, by the id it named them under.</summary>
    [Fact]
    public async Task StopSuggesting_TellsTheInstanceToStopSuggestingThePickedPerson()
    {
        var (fakes, opened) = await OnDiscover();

        await opened.StopSuggesting();
        fakes.Host.Drain();

        var dismissed = Assert.Single(fakes.Suggestions.Dismissals);
        Assert.Equal("1", dismissed.AccountId);
        Assert.Equal("personal", dismissed.Profile);
    }

    /// <summary>
    ///     A second press asks for nothing: there is no un-dismiss endpoint, so a second <c>d</c> is a no-op rather
    ///     than a second call saying what the first one said.
    /// </summary>
    [Fact]
    public async Task StopSuggesting_AsksNothingASecondTime()
    {
        var (fakes, opened) = await OnDiscover();

        await opened.StopSuggesting();
        fakes.Host.Drain();

        await opened.StopSuggesting();
        fakes.Host.Drain();

        Assert.Single(fakes.Suggestions.Dismissals);
    }

    /// <summary>And the row stays where it is, walkable and still open to <c>⏎</c> and <c>F</c>.</summary>
    [Fact]
    public async Task StopSuggesting_LeavesTheRowWhereItIs()
    {
        var (fakes, opened) = await OnDiscover(
            ASuggestion.With(AnAccount.With(address: "alice@hachyderm.io", id: "1")),
            ASuggestion.With(AnAccount.With(address: "ben@hachyderm.io", id: "2")));

        await opened.StopSuggesting();
        fakes.Host.Drain();

        var screen = Assert.IsType<DiscoverScreen>(opened.Screen);

        Assert.Equal(["alice@hachyderm.io", "ben@hachyderm.io"], screen.People.Select(person => person.Address));
        Assert.Equal("alice@hachyderm.io", screen.PickedPerson?.Address);
        Assert.True(screen.IsDismissed(screen.PickedPerson!.Id));
    }

    /// <summary>
    ///     A tie forgets what Discover held: an instance never suggests somebody already followed, so a held copy is
    ///     one the instance would no longer have served.
    /// </summary>
    [Fact]
    public async Task Follow_ForgetsWhatDiscoverHeld()
    {
        var (fakes, opened) = await OnDiscover();

        await opened.Tie(AccountTie.Follow);
        fakes.Host.Drain();

        LeaveAndComeBack(fakes, opened);

        Assert.Equal(2, fakes.Suggestions.Reads.Count);
    }

    /// <summary>And so does a dismissal, for the same reason.</summary>
    [Fact]
    public async Task StopSuggesting_ForgetsWhatDiscoverHeld()
    {
        var (fakes, opened) = await OnDiscover();

        await opened.StopSuggesting();
        fakes.Host.Drain();

        LeaveAndComeBack(fakes, opened);

        Assert.Equal(2, fakes.Suggestions.Reads.Count);
    }

    /// <summary>
    ///     Without either of those, walking away and back inside the minute costs nothing — the cache Discover gets
    ///     for free, and what the two evictions above are measured against.
    /// </summary>
    [Fact]
    public async Task Arriving_AsksNothingForACopyItIsStillHolding()
    {
        var (fakes, opened) = await OnDiscover();

        LeaveAndComeBack(fakes, opened);

        Assert.Single(fakes.Suggestions.Reads);
    }

    /// <summary><c>g</c> evicts what is held and asks again, which is what a refresh is at every destination.</summary>
    [Fact]
    public async Task Refresh_AsksTheInstanceAgain()
    {
        var (fakes, opened) = await OnDiscover();

        await opened.Refresh();
        fakes.Host.Drain();

        Assert.Equal(2, fakes.Suggestions.Reads.Count);
    }

    /// <summary>
    ///     A follow made on the account screen opened from a Discover row reaches the row underneath it: the two are
    ///     on the stack together, so <c>esc</c> must not land back on a row saying the opposite of what is true.
    /// </summary>
    [Fact]
    public async Task Follow_ReachesTheDiscoverRowUnderTheAccountScreenOpenedFromIt()
    {
        var (fakes, opened) = await OnDiscover();

        fakes.Accounts.Becoming = AnAccount.With(
            address: "alice@hachyderm.io",
            id: "1",
            standing: AnAccount.Standing(following: true));

        await opened.OpenPerson();
        fakes.Host.Drain();

        await opened.Tie(AccountTie.Follow);
        fakes.Host.Drain();

        opened.Back();

        var screen = Assert.IsType<DiscoverScreen>(opened.Screen);
        Assert.True(screen.PickedPerson?.Standing?.Following);
    }

    /// <summary>
    ///     A dismissal says nothing on the status row: the row itself now says <c>· dismissed</c>, and the status row
    ///     holds either a notice or the keys and never both.
    /// </summary>
    [Fact]
    public async Task StopSuggesting_SaysNothingOverTheKeysTheScreenAnswersTo()
    {
        var (fakes, opened) = await OnDiscover();

        await opened.StopSuggesting();
        fakes.Host.Drain();

        Assert.Null(opened.Notice);
        Assert.Contains(opened.Keys, key => key is { Key: "d", Does: "dismiss" });
    }

    /// <summary><c>⏎</c> opens the account screen of whoever is picked out, the same screen <c>a</c> opens from a feed.</summary>
    [Fact]
    public async Task OpenPerson_OpensTheAccountScreenOfWhoeverIsPickedOut()
    {
        var (fakes, opened) = await OnDiscover();

        await opened.OpenPerson();
        fakes.Host.Drain();

        Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Contains(fakes.Accounts.Reads, read => read.Account.Text == "alice@hachyderm.io");
    }

    /// <summary>And a dismissed row opens as readily: dismissing says "stop suggesting", not "hide".</summary>
    [Fact]
    public async Task OpenPerson_OpensADismissedRowToo()
    {
        var (fakes, opened) = await OnDiscover();

        await opened.StopSuggesting();
        fakes.Host.Drain();

        await opened.OpenPerson();
        fakes.Host.Drain();

        Assert.IsType<AccountScreen>(opened.Screen);
    }

    /// <summary>Discover's own entry on the rail, found by kind rather than by counting the entries by hand.</summary>
    private static Destination Discover(Shell opened) =>
        opened.Rail.Destinations.Single(destination => destination.Kind == DestinationKind.Discover);

    /// <summary>Walking out to another destination and back, which is what the cache is asked about.</summary>
    private static void LeaveAndComeBack(AShell fakes, Shell opened)
    {
        opened.Rail.GoTo(DestinationKind.Home);
        fakes.Host.Drain();

        opened.Rail.GoTo(DestinationKind.Discover);
        fakes.Host.Drain();
    }

    /// <summary>A shell that has arrived at Discover, holding <paramref name="suggested" />.</summary>
    private static async Task<(AShell Fakes, Shell Opened)> OnDiscover(params Suggestion[] suggested)
    {
        var fakes = new AShell
        {
            Suggestions = FakeFollowSuggestions.Offering(
                suggested.Length == 0
                    ? [ASuggestion.With(AnAccount.With(address: "alice@hachyderm.io", id: "1"))]
                    : suggested),
        };

        var opened = await fakes.Opened();

        opened.Rail.GoTo(DestinationKind.Discover);
        fakes.Host.Drain();

        return (fakes, opened);
    }
}
