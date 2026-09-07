using Wooly.Core.Accounts;
using Wooly.Core.Relationships;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The screen <c>w</c> opens: everyone an account follows, or everyone who follows it — one screen, either side,
///     anybody's account (#180). Built with no terminal and no shell around it.
/// </summary>
public class FollowsScreenTests
{
    /// <summary>The breadcrumb says whose list this is and which side of it, since one screen draws both.</summary>
    [Theory]
    [InlineData(FollowSide.Following, "@maria@fosstodon.org following")]
    [InlineData(FollowSide.Followers, "@maria@fosstodon.org followers")]
    public void Crumb_SaysWhoseListAndWhichSide(FollowSide side, string crumb) =>
        Assert.Equal(crumb, Of(side).Crumb);

    /// <summary>
    ///     The mode is picked from the count the account already carries, before anything is fetched: a list this
    ///     client can hold whole is held whole, and one it cannot is browsed.
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1_999, true)]
    [InlineData(2_000, false)]
    [InlineData(877_000, false)]
    public void Holds_IsSettledByTheCountBeforeAnythingIsRead(int followers, bool held)
    {
        var screen = new FollowsScreen(AnAccount.With(followers: followers), FollowSide.Followers, mine: false);

        Assert.Equal(held, screen.Holds);
        Assert.Empty(screen.People);
    }

    /// <summary>And it is the count of the side being listed, not of the other one.</summary>
    [Fact]
    public void Holds_ReadsTheCountOfTheSideBeingListed()
    {
        var account = AnAccount.With(followers: 900_000, following: 12);

        Assert.False(new FollowsScreen(account, FollowSide.Followers, mine: false).Holds);
        Assert.True(new FollowsScreen(account, FollowSide.Following, mine: false).Holds);
    }

    /// <summary>People arrive a page at a time and land below whoever is picked out, which does not move.</summary>
    [Fact]
    public void Arrived_LeavesThePickWhereTheReaderPutIt()
    {
        var screen = Of();

        screen.Arrived([Person("a"), Person("b"), Person("c")], more: true);
        screen.Move(2);

        Assert.Equal("c", screen.PickedPerson?.Author);

        screen.Arrived([Person("a"), Person("b"), Person("c"), Person("d")], more: false);

        Assert.Equal("c", screen.PickedPerson?.Author);
        Assert.Equal(4, screen.Read);
    }

    /// <summary>A page that repeats what is already held adds only what is new, a re-read being how a page is asked for.</summary>
    [Fact]
    public void Arrived_AddsOnlyWhoIsNew()
    {
        var screen = Of();

        screen.Arrived([Person("a"), Person("b")], more: true);
        screen.Arrived([Person("a"), Person("b"), Person("c")], more: false);

        Assert.Equal(["a", "b", "c"], screen.People.Select(person => person.Author));
    }

    /// <summary>
    ///     A browsed list asks for more once the reader walks onto the last of what has been read, and stops asking
    ///     once there is no more to read.
    /// </summary>
    [Fact]
    public void WantsMore_IsWalkingOntoTheEndOfWhatHasBeenRead()
    {
        var screen = new FollowsScreen(AnAccount.With(followers: 877_000), FollowSide.Followers, mine: false);

        screen.Arrived([Person("a"), Person("b")], more: true);

        Assert.False(screen.WantsMore);

        screen.Move(1);

        Assert.True(screen.WantsMore);
    }

    /// <summary>A held list never asks: the whole of it is on its way already.</summary>
    [Fact]
    public void WantsMore_IsNeverTrueOnAListHeldWhole()
    {
        var screen = Of();

        screen.Arrived([Person("a")], more: true);
        screen.Move(1);

        Assert.False(screen.WantsMore);
    }

    /// <summary>
    ///     <c>f</c> opens the prompt and the screen starts taking letters, which is what swaps the status row to the
    ///     letters-are-text keymap (ADR-0015).
    /// </summary>
    [Fact]
    public void Filtering_TakesLetters()
    {
        var screen = Of();

        Assert.False(screen.IsTyping);

        screen.Filtering();

        Assert.True(screen.IsTyping);

        screen.Type('m');
        screen.Type('a');

        Assert.Equal("ma", screen.Filter);
    }

    /// <summary>Typing narrows on every keystroke rather than on ⏎, which is what makes it a filter and not a search.</summary>
    [Fact]
    public void Filter_NarrowsOnEveryLetter()
    {
        var screen = Of();

        screen.Arrived([Person("Maria"), Person("Ben"), Person("Marcus")], more: false);
        screen.Filtering();
        screen.Type('m');

        Assert.Equal(["Maria", "Marcus"], screen.Shown.Select(person => person.Author));

        screen.Type('a');
        screen.Type('r');
        screen.Type('i');

        Assert.Equal(["Maria"], screen.Shown.Select(person => person.Author));
    }

    /// <summary>By handle as well as by name, either being what a reader half-remembers about somebody.</summary>
    [Fact]
    public void Filter_MatchesTheHandleToo()
    {
        var screen = Of();

        screen.Arrived([Person("Maria", "maria@fosstodon.org"), Person("Ben", "ben@hachyderm.io")], more: false);
        screen.Filtering();
        screen.Type('h');
        screen.Type('a');

        Assert.Equal(["Ben"], screen.Shown.Select(person => person.Author));
    }

    /// <summary>⏎ hands the screen back to walking with what was typed still narrowing it.</summary>
    [Fact]
    public void Done_KeepsTheFilterAndStopsTakingLetters()
    {
        var screen = Of();

        screen.Arrived([Person("Maria"), Person("Ben")], more: false);
        screen.Filtering();
        screen.Type('m');
        screen.Done();

        Assert.False(screen.IsTyping);
        Assert.Equal("m", screen.Filter);
        Assert.Equal(["Maria"], screen.Shown.Select(person => person.Author));
    }

    /// <summary>And esc takes it off, which puts everyone back on the screen.</summary>
    [Fact]
    public void Clear_PutsEveryoneBack()
    {
        var screen = Of();

        screen.Arrived([Person("Maria"), Person("Ben")], more: false);
        screen.Filtering();
        screen.Type('m');

        Assert.True(screen.Clear());

        Assert.False(screen.IsTyping);
        Assert.Equal(string.Empty, screen.Filter);
        Assert.Equal(2, screen.Shown.Count);

        // And nothing to clear the second time, which is what lets esc go on to mean back.
        Assert.False(screen.Clear());
    }

    /// <summary>People who arrive while a filter is on are narrowed by it too, rather than appearing through it.</summary>
    [Fact]
    public void Filter_NarrowsWhoArrivesAfterIt()
    {
        var screen = Of();

        screen.Arrived([Person("Maria")], more: true);
        screen.Filtering();
        screen.Type('m');
        screen.Done();

        screen.Arrived([Person("Maria"), Person("Ben"), Person("Marcus")], more: false);

        Assert.Equal(["Maria", "Marcus"], screen.Shown.Select(person => person.Author));
        Assert.Equal(3, screen.Read);
    }

    /// <summary>A browsed list is not filterable at all: what has been read is too little of it to say "no matches".</summary>
    [Fact]
    public void Filtering_DoesNothingOnAListTooLargeToHold()
    {
        var screen = new FollowsScreen(AnAccount.With(followers: 877_000), FollowSide.Followers, mine: false);

        screen.Arrived([Person("Maria"), Person("Ben")], more: true);
        screen.Filtering();

        Assert.False(screen.IsTyping);

        screen.Type('m');

        Assert.Equal(string.Empty, screen.Filter);
        Assert.Equal(2, screen.Shown.Count);
    }

    /// <summary>
    ///     The status row: what the ticket names, in the order it names them — and <c>s</c> saying which side it swaps
    ///     to rather than which side this is.
    /// </summary>
    [Theory]
    [InlineData(FollowSide.Following, "j/k:person ⏎:open f:filter s:followers g:refresh ↓/↑:row esc:back ?:keys")]
    [InlineData(FollowSide.Followers, "j/k:person ⏎:open f:filter s:following g:refresh ↓/↑:row esc:back ?:keys")]
    public void Keys_SayWhatTheScreenAnswersTo(FollowSide side, string row)
    {
        var screen = Of(side);

        screen.Arrived([Person("a")], more: false);

        Assert.Equal(row, string.Join(' ', screen.Keys));
    }

    /// <summary>A browsed list loses <c>f</c>, there being no honest filter to offer.</summary>
    [Fact]
    public void Keys_LoseTheFilterOnAListTooLargeToHold()
    {
        var screen = new FollowsScreen(AnAccount.With(followers: 877_000), FollowSide.Followers, mine: false);

        screen.Arrived([Person("a")], more: true);

        Assert.Equal(
            "j/k:person ⏎:open s:following g:refresh ↓/↑:row esc:back ?:keys",
            string.Join(' ', screen.Keys));
    }

    /// <summary>And while the prompt is taking letters, the row collapses to what the prompt answers to.</summary>
    [Fact]
    public void Keys_CollapseWhileTheFilterIsTakingLetters()
    {
        var screen = Of();

        screen.Arrived([Person("a")], more: false);
        screen.Filtering();

        Assert.Equal("⏎:done esc:clear", string.Join(' ', screen.Keys));
    }

    /// <summary>
    ///     An empty list stops announcing the key that needs somebody picked out (#195) — and goes on announcing
    ///     <c>f</c>, which opens a prompt rather than acting on anybody, and is most wanted where a filter has just
    ///     narrowed the list to nobody.
    /// </summary>
    [Fact]
    public void Keys_ComeOffAnEmptyRow()
    {
        var screen = Of();

        screen.Arrived([], more: false);

        Assert.Equal(
            "j/k:person f:filter s:followers g:refresh ↓/↑:row esc:back ?:keys",
            string.Join(' ', screen.Keys));
    }

    /// <summary>The count says how far the reading has got while it is still going, and what was found once it stops.</summary>
    [Fact]
    public void Lines_SayHowFarTheReadingHasGot()
    {
        var screen = new FollowsScreen(AnAccount.With(following: 2), FollowSide.Following, mine: false);

        screen.Arrived([Person("a")], more: true);

        Assert.Contains("1 of 2 read", Rows(screen));

        screen.Arrived([Person("a"), Person("b")], more: false);

        Assert.Contains("2 following", Rows(screen));
    }

    /// <summary>
    ///     And a read a rate limit stopped short goes on saying both numbers, rather than putting the whole count
    ///     over half a list: the reader is looking at 80 of 187 and must not be told they are looking at 187.
    /// </summary>
    [Fact]
    public void Lines_GoOnSayingBothNumbersWhereTheReadingStoppedShort()
    {
        var screen = Of();

        screen.Arrived([Person("a"), Person("b")], more: false);

        Assert.Contains("2 of 187 read", Rows(screen));
    }

    /// <summary>A browsed list always says both numbers, since what has been read is never the whole of it.</summary>
    [Fact]
    public void Lines_SayBothNumbersOnAListTooLargeToHold()
    {
        var screen = new FollowsScreen(AnAccount.With(followers: 877_000), FollowSide.Followers, mine: false);

        screen.Arrived([Person("a"), Person("b")], more: true);

        Assert.Contains("2 of 877,000 read", Rows(screen));
    }

    /// <summary>
    ///     A filter that has narrowed a list still being read says so, so that a thin result reads as still reading
    ///     rather than as nobody matching.
    /// </summary>
    [Fact]
    public void Lines_SayTheReadingIsStillGoingUnderAFilter()
    {
        var screen = Of();

        screen.Arrived([Person("Maria"), Person("Ben")], more: true);
        screen.Filtering();
        screen.Type('m');

        Assert.Contains("1 matching · 2 of 187 read", Rows(screen));
    }

    /// <summary>The prompt is drawn only where there is a filter to show, the screen arriving walkable rather than typing.</summary>
    [Fact]
    public void Lines_DrawThePromptOnlyWhereThereIsAFilter()
    {
        var screen = Of();

        screen.Arrived([Person("Maria")], more: false);

        Assert.DoesNotContain("Filter:", Rows(screen));

        screen.Filtering();
        screen.Type('m');

        Assert.Contains("Filter: m▌", Rows(screen));

        screen.Done();

        Assert.Contains("Filter: m", Rows(screen));
    }

    /// <summary>One rule under the prompt and none between the people, at 900 of whom it would be 900 more rows.</summary>
    [Fact]
    public void Lines_RuleOnceRatherThanBetweenEveryone()
    {
        var screen = Of();

        screen.Arrived([Person("a"), Person("b"), Person("c")], more: false);

        var rules = screen.Lines(new Drawing(61, AShell.Now)).Count(line => line.Spans.Any(span => span.Text.Contains('─')));

        Assert.Equal(1, rules);
    }

    /// <summary>A row says where the reader stands with them, and leaves out what the list itself already says.</summary>
    [Theory]
    [InlineData(false, FollowSide.Following, "▌Maria @maria@fosstodon.org  following · follows you")]
    [InlineData(true, FollowSide.Following, "▌Maria @maria@fosstodon.org  follows you")]
    [InlineData(true, FollowSide.Followers, "▌Maria @maria@fosstodon.org  following")]
    public void Lines_DrawAPersonAsTheListImplies(bool mine, FollowSide side, string row)
    {
        var screen = new FollowsScreen(AnAccount.With(), side, mine);

        screen.Arrived(
            [
                AnAccount.With(
                    author: "Maria",
                    address: "maria@fosstodon.org",
                    standing: AnAccount.Standing(following: true, followedBy: true)),
            ],
            more: false);

        Assert.Contains(row, Rows(screen));
    }

    /// <summary>The screen answers to <c>g</c>, which is what the contract's refresh table says of it.</summary>
    [Fact]
    public void Refreshes_IsAskedAgain() => Assert.True(Of().Refreshes);

    /// <summary>Whoever is picked out is who <c>⏎</c> opens, and nobody where nobody is on the screen.</summary>
    [Fact]
    public void PickedPerson_IsNobodyOnAnEmptyList()
    {
        var screen = Of();

        Assert.Null(screen.PickedPerson);

        screen.Arrived([Person("a")], more: false);

        Assert.Equal("a", screen.PickedPerson?.Author);
    }

    /// <summary>A list of people is not a list of posts, so no key that acts on one is announced here.</summary>
    [Fact]
    public void Picked_IsNeverAPost()
    {
        var screen = Of();

        screen.Arrived([Person("a")], more: false);

        Assert.Null(screen.Picked);
    }

    /// <summary>
    ///     What the shell has to say about the list is drawn on the screen, not said on the status row: the row holds
    ///     either a notice or the keys, and a list with nobody on it has nothing to walk that would ever take one
    ///     down again.
    /// </summary>
    [Fact]
    public void Lines_CarryTheNoticeAboutTheList()
    {
        var screen = Of();

        screen.Arrived([], more: false, notice: "They follow nobody yet.");

        Assert.Contains("They follow nobody yet.", Rows(screen));
        Assert.Equal(
            "j/k:person f:filter s:followers g:refresh ↓/↑:row esc:back ?:keys",
            string.Join(' ', screen.Keys));
    }

    /// <summary>And no count over a list nobody has arrived on, which would be a sum a reader has to work out.</summary>
    [Fact]
    public void Lines_SayNoCountWhereNobodyHasArrived()
    {
        var screen = Of();

        screen.Arrived([], more: false, notice: "They follow nobody yet.");

        Assert.DoesNotContain("0 of 187 read", Rows(screen));
    }

    /// <summary>
    ///     An account whose profile says it follows people while the instance lists none of them is not an account
    ///     that follows nobody: both numbers are said and no cause is guessed at, an account being free to keep who
    ///     it follows to itself.
    /// </summary>
    [Theory]
    [InlineData(false, FollowSide.Following, 5, "Their profile says 5 following, but the instance listed nobody.")]
    [InlineData(true, FollowSide.Followers, 5, "Your profile says 5 followers, but the instance listed nobody.")]
    [InlineData(false, FollowSide.Following, 0, "They follow nobody yet.")]
    [InlineData(true, FollowSide.Following, 0, "You follow nobody yet.")]
    [InlineData(false, FollowSide.Followers, 0, "Nobody follows them yet.")]
    [InlineData(true, FollowSide.Followers, 0, "Nobody follows you yet.")]
    public void Nobody_TellsAnEmptyListFromOneTheInstanceWouldNotServe(
        bool mine,
        FollowSide side,
        int total,
        string said)
    {
        var account = AnAccount.With(followers: total, following: total);

        Assert.Equal(said, new FollowsScreen(account, side, mine).Nobody);
    }

    /// <summary>The rows this screen draws, as the strings a reader sees.</summary>
    private static IReadOnlyList<string> Rows(FollowsScreen screen) =>
        [.. screen.Lines(new Drawing(61, AShell.Now)).Select(line => string.Concat(line.Spans.Select(span => span.Text)).TrimEnd())];

    /// <summary>Somebody on the list, named so a test can tell them apart.</summary>
    private static Account Person(string author, string? address = null) =>
        AnAccount.With(author: author, address: address ?? $"{author.ToLowerInvariant()}@here.social", id: author);

    /// <summary>Maria's list, of the side named, read by somebody who is not her.</summary>
    private static FollowsScreen Of(FollowSide side = FollowSide.Following) =>
        new(AnAccount.With(address: "maria@fosstodon.org", author: "Maria"), side, mine: false);
}
