using Wooly.Core.Accounts;
using Wooly.Core.Relationships;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     What <c>w</c> opens and <c>s</c> swaps: everyone an account follows, or everyone who follows it — read a page
///     at a time, and drawn with where the reader stands with each of them (#180).
/// </summary>
public class ShellFollowsTests
{
    /// <summary><c>w</c> opens the following side of whichever account is showing.</summary>
    [Fact]
    public async Task OpenFollows_OpensTheFollowingSideOfTheAccountShowing()
    {
        var (fakes, opened) = await OnAFollowList();

        var follows = Assert.IsType<FollowsScreen>(opened.Screen);

        Assert.Equal(FollowSide.Following, follows.Side);
        Assert.Equal("@ben@hachyderm.io following", follows.Crumb);

        var listed = Assert.Single(Listed(fakes));
        Assert.Equal(FollowSide.Following, listed.Side);
        Assert.Equal("ben@hachyderm.io", listed.Account?.Text);
    }

    /// <summary>And it means nothing off an account screen, there being nobody whose list it would be.</summary>
    [Fact]
    public async Task OpenFollows_DoesNothingOffTheAccountScreen()
    {
        var fakes = new AShell();
        var opened = await fakes.Opened();

        await opened.OpenFollows();
        fakes.Host.Drain();

        Assert.IsNotType<FollowsScreen>(opened.Screen);
        Assert.Empty(Listed(fakes));
    }

    /// <summary>
    ///     <c>s</c> swaps to the other side in place: a new crumb over the same stack, rather than a second screen on
    ///     top of the first. Somebody else's followers costs <c>w</c> then <c>s</c>.
    /// </summary>
    [Fact]
    public async Task SwapSide_ChangesSidesWithoutGrowingTheStack()
    {
        var (fakes, opened) = await OnAFollowList();

        var depth = opened.Depth;

        await opened.SwapSide();
        fakes.Host.Drain();

        var follows = Assert.IsType<FollowsScreen>(opened.Screen);

        Assert.Equal(FollowSide.Followers, follows.Side);
        Assert.Equal("@ben@hachyderm.io followers", follows.Crumb);
        Assert.Equal(depth, opened.Depth);
        Assert.Equal(FollowSide.Followers, Listed(fakes)[^1].Side);
    }

    /// <summary>And the swap takes the filter with it, being a fresh list rather than the same one re-labelled.</summary>
    [Fact]
    public async Task SwapSide_LeavesTheFilterBehind()
    {
        var (fakes, opened) = await OnAFollowList();

        opened.Filter();
        opened.Type('m');

        await opened.SwapSide();
        fakes.Host.Drain();

        var follows = Assert.IsType<FollowsScreen>(opened.Screen);

        Assert.Equal(string.Empty, follows.Filter);
        Assert.False(follows.IsTyping);
    }

    /// <summary><c>esc</c> hands back the account screen it was opened from, with its page intact.</summary>
    [Fact]
    public async Task Back_ReturnsToTheAccountScreen()
    {
        var (_, opened) = await OnAFollowList();

        opened.Back();

        Assert.IsType<AccountScreen>(opened.Screen);
    }

    /// <summary>
    ///     And a filter is a level of its own on the way out: the first <c>esc</c> puts the whole list back and the
    ///     next one leaves.
    /// </summary>
    [Fact]
    public async Task Back_TakesTheFilterOffBeforeItLeaves()
    {
        var (_, opened) = await OnAFollowList();

        opened.Filter();
        opened.Type('z');

        opened.Back();

        var follows = Assert.IsType<FollowsScreen>(opened.Screen);
        Assert.Equal(string.Empty, follows.Filter);

        opened.Back();

        Assert.IsType<AccountScreen>(opened.Screen);
    }

    /// <summary>
    ///     A page is asked for with the standing of everyone new on it, in the one call that endpoint takes many ids
    ///     for — so the rows say where the reader stands without one call a row.
    /// </summary>
    [Fact]
    public async Task OpenFollows_AsksWhereTheReaderStandsWithThePageAtOnce()
    {
        var (fakes, opened) = await OnAFollowList(
            [AnAccount.With(id: "7", author: "Maria", address: "maria@fosstodon.org")],
            configure: accounts => accounts.Stands = AnAccount.Standing(followedBy: true));

        var stood = Assert.Single(fakes.Accounts.Standings);
        Assert.Equal(["7"], stood.AccountIds);

        var follows = Assert.IsType<FollowsScreen>(opened.Screen);

        Assert.True(follows.People[0].Standing?.FollowedBy);
        Assert.Contains("▌Maria @maria@fosstodon.org  follows you", Rows(follows));
    }

    /// <summary>
    ///     A page whose standing call was refused draws rows with nothing said about a tie, rather than rows implying
    ///     there is none.
    /// </summary>
    [Fact]
    public async Task OpenFollows_DrawsSilentRowsWhereTheStandingWasNeverAnswered()
    {
        var (_, opened) = await OnAFollowList(
            [AnAccount.With(author: "Maria", address: "maria@fosstodon.org")],
            configure: accounts => accounts.Stands = null);

        var follows = Assert.IsType<FollowsScreen>(opened.Screen);

        Assert.Null(follows.People[0].Standing);
        Assert.Contains("▌Maria @maria@fosstodon.org", Rows(follows));
    }

    /// <summary>⏎ opens the account screen of whoever is picked out, the same screen <c>a</c> opens from a feed.</summary>
    [Fact]
    public async Task OpenPerson_OpensTheAccountPickedOut()
    {
        var (fakes, opened) = await OnAFollowList([AnAccount.With(address: "maria@fosstodon.org")]);

        await opened.OpenPerson();
        fakes.Host.Drain();

        Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("maria@fosstodon.org", fakes.Accounts.Reads[^1].Account.Text);
    }

    /// <summary>
    ///     A list too large to hold whole is browsed: the first page and no more, until the reader walks onto the end
    ///     of it and the next 80 are asked for.
    /// </summary>
    [Fact]
    public async Task Walking_AsksForTheNextPageAtTheEndOfABrowsedList()
    {
        var (fakes, opened) = await OnAFollowList(
            whose: AnAccount.With(address: "ben@hachyderm.io", following: 877_000),
            listing: APage());

        var follows = Assert.IsType<FollowsScreen>(opened.Screen);

        Assert.False(follows.Holds);
        Assert.Equal(80, Listed(fakes)[^1].Limit);

        var asked = Listed(fakes).Count;

        // Onto the last of what was read, which is where the next page is wanted — and not before.
        opened.Move(78);
        fakes.Host.Drain();

        Assert.Equal(asked, Listed(fakes).Count);

        opened.Move(1);
        fakes.Host.Drain();

        Assert.Equal(asked + 1, Listed(fakes).Count);
        Assert.Equal(160, Listed(fakes)[^1].Limit);
    }

    /// <summary>A list held whole reads the first page and then the rest of it, behind whoever is already reading.</summary>
    [Fact]
    public async Task OpenFollows_ReadsTheRestOfAHeldListBehindTheReader()
    {
        var (fakes, opened) = await OnAFollowList(
            whose: AnAccount.With(address: "ben@hachyderm.io", following: 187),
            listing: APage());

        var follows = Assert.IsType<FollowsScreen>(opened.Screen);

        Assert.True(follows.Holds);
        Assert.Equal([80, 187], Listed(fakes).Select(listed => listed.Limit));
        Assert.False(follows.More);
    }

    /// <summary>
    ///     <c>g</c> reads the list again with the filter cleared and the pick back at the top, which is what a refresh
    ///     is everywhere else too.
    /// </summary>
    [Fact]
    public async Task Refresh_ClearsTheFilterAndReadsAgain()
    {
        var (fakes, opened) = await OnAFollowList();

        opened.Filter();
        opened.Type('z');
        opened.FilterDone();

        var asked = Listed(fakes).Count;

        await opened.Refresh();
        fakes.Host.Drain();

        var follows = Assert.IsType<FollowsScreen>(opened.Screen);

        Assert.Equal(string.Empty, follows.Filter);
        Assert.Equal(asked + 1, Listed(fakes).Count);
    }

    /// <summary>
    ///     A list read whole is worth handing back for a minute, so walking out of one and straight back in asks the
    ///     instance nothing.
    /// </summary>
    [Fact]
    public async Task OpenFollows_HandsBackWhatItLastHeldForAMinute()
    {
        var (fakes, opened) = await OnAFollowList();

        opened.Back();

        var asked = Listed(fakes).Count;

        await opened.OpenFollows();
        fakes.Host.Drain();

        Assert.IsType<FollowsScreen>(opened.Screen);
        Assert.Equal(asked, Listed(fakes).Count);
    }

    /// <summary>And asks again once that has gone stale, the age being the whole of the cache's judgement.</summary>
    [Fact]
    public async Task OpenFollows_AsksAgainOnceWhatItHeldIsOld()
    {
        var (fakes, opened) = await OnAFollowList();

        opened.Back();

        var asked = Listed(fakes).Count;

        fakes.Clock.Advance(TimeSpan.FromMinutes(2));

        await opened.OpenFollows();
        fakes.Host.Drain();

        Assert.Equal(asked + 1, Listed(fakes).Count);
    }

    /// <summary>
    ///     Your own list knows it is yours, which is what lets its rows leave out what it has already said of
    ///     everyone on them.
    /// </summary>
    [Fact]
    public async Task OpenFollows_KnowsWhoseAccountItIs()
    {
        var (_, mine) = await OnAFollowList(whose: AnAccount.With(address: "jeff@mastodon.social"));

        Assert.True(Assert.IsType<FollowsScreen>(mine.Screen).Mine);

        var (_, theirs) = await OnAFollowList();

        Assert.False(Assert.IsType<FollowsScreen>(theirs.Screen).Mine);
    }

    /// <summary>A list with nobody on it says so, rather than drawing as an empty screen nobody can read.</summary>
    [Fact]
    public async Task OpenFollows_SaysWhereNobodyIsOnTheList()
    {
        var fakes = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
            Accounts = FakeAccountRelationships.HoldingNobody(),
        };

        var opened = await OnTheAccountScreen(fakes);

        await opened.OpenFollows();
        fakes.Host.Drain();

        Assert.Equal("They follow nobody yet.", opened.Notice);
    }

    /// <summary>
    ///     Every follow list this shell asked for, which is not every list it asked for: the rail counts the follows
    ///     waiting through the same port, and those name no side.
    /// </summary>
    private static IReadOnlyList<FakeAccountRelationships.Listed> Listed(AShell fakes) =>
        [.. fakes.Accounts.Lists.Where(listed => listed.Side is not null)];

    /// <summary>A full page of people, which is what tells the shell the instance has more to give.</summary>
    private static Account[] APage() =>
        [.. Enumerable.Range(0, 80).Select(at => AnAccount.With(id: at.ToString(), address: $"person{at}@here.social"))];

    /// <summary>The rows a screen draws, as the strings a reader sees.</summary>
    private static IReadOnlyList<string> Rows(Screen screen) =>
        [.. screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text)];

    /// <summary>
    ///     A shell standing on Ben's following list, opened the way a reader opens one: onto his account from the
    ///     timeline, then <c>w</c>.
    /// </summary>
    private static async Task<(AShell Fakes, Shell Opened)> OnAFollowList(
        Account[]? listing = null,
        Account? whose = null,
        Action<FakeAccountRelationships>? configure = null)
    {
        var fakes = Fakes(whose ?? AnAccount.With(address: "ben@hachyderm.io"), listing ?? [AnAccount.With()]);

        configure?.Invoke(fakes.Accounts);

        var opened = await OnTheAccountScreen(fakes);

        await opened.OpenFollows();
        fakes.Host.Drain();

        return (fakes, opened);
    }

    /// <summary>Fakes answering about <paramref name="whose" /> and listing <paramref name="listing" />.</summary>
    private static AShell Fakes(Account whose, Account[] listing) => new()
    {
        Timelines = FakeTimelineReader.Holding(APost.With(id: "110", account: "ben@hachyderm.io")),
        Accounts = FakeAccountRelationships.Holding(whose, listing),
    };

    /// <summary>A shell drilled into the account of whoever wrote the post on the timeline.</summary>
    private static async Task<Shell> OnTheAccountScreen(AShell fakes)
    {
        var opened = await fakes.Opened();

        await opened.OpenAuthor();

        fakes.Host.Drain();

        return opened;
    }
}
