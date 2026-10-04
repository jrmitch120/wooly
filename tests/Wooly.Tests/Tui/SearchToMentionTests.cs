using Wooly.Core.Accounts;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The instance's own search of the profile's follows, as people a post can mention (#322): asked only where the
///     follow list read is not the whole of it and what is known locally falls short — and then only once typing has
///     paused, once per pause, and once per query in a session.
/// </summary>
public class SearchToMentionTests
{
    /// <summary>A follow list read whole is everybody the profile follows, so there is nothing to search for.</summary>
    [Fact]
    public async Task AWholeFollowListIsNeverSearched()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(null, Account("maria@b.social", "Maria")),
            Search = FakeInstanceSearch.Finding(accounts: [Account("mark@a.social", "Mark")]),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("ma");
        shell.Host.Settle();

        Assert.Empty(shell.Search.FollowedSearches);
        Assert.Equal(["maria@b.social"], Addresses(opened, "ma"));
    }

    /// <summary>
    ///     A follow list cut off at its cap may be missing anybody, so a short answer is searched for — once typing has
    ///     paused, and not while the letters are still arriving.
    /// </summary>
    [Fact]
    public async Task ACappedFollowListIsSearchedOnceTypingPauses()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(null, Capped()),
            Search = FakeInstanceSearch.Finding(accounts: [Account("maria@b.social", "Maria")]),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("m");
        shell.Host.Drain();
        opened.PeopleMatching("ma");
        opened.PeopleMatching("mar");

        Assert.Empty(shell.Search.FollowedSearches);

        shell.Host.Settle();

        Assert.Equal([("personal", "mar")], shell.Search.FollowedSearches);
    }

    /// <summary>A follow list still arriving may not have reached anybody yet, so a short answer is searched for.</summary>
    [Fact]
    public async Task AFollowListStillArrivingIsSearched()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.StillListing(Account("mabel@c.social", "Mabel")),
            Search = FakeInstanceSearch.Finding(accounts: [Account("maria@b.social", "Maria")]),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("ma");
        shell.Host.Settle();

        Assert.Equal([("personal", "ma")], shell.Search.FollowedSearches);
        Assert.Equal(["mabel@c.social", "maria@b.social"], Addresses(opened, "ma"));
    }

    /// <summary>A follow list a refusal stopped is no more whole than one still arriving.</summary>
    [Fact]
    public async Task ARefusedFollowListIsSearched()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.RateLimitedAfter(Account("mabel@c.social", "Mabel")),
            Search = FakeInstanceSearch.Finding(accounts: [Account("maria@b.social", "Maria")]),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("ma");
        shell.Host.Settle();

        Assert.Single(shell.Search.FollowedSearches);
    }

    /// <summary>Five matches already known fill the list, so nothing is searched for however incomplete the follows.</summary>
    [Fact]
    public async Task FiveLocalMatchesAreNeverSearchedPast()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.StillListing(
                Account("ma1@c.social", "Ma One"),
                Account("ma2@c.social", "Ma Two"),
                Account("ma3@c.social", "Ma Three"),
                Account("ma4@c.social", "Ma Four"),
                Account("ma5@c.social", "Ma Five")),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("ma");
        shell.Host.Settle();

        Assert.Empty(shell.Search.FollowedSearches);
    }

    /// <summary>
    ///     What the search finds is offered alongside what was known, each person once, and an open list is told to
    ///     redraw with it.
    /// </summary>
    [Fact]
    public async Task WhatTheSearchFindsJoinsTheList_WithoutDuplicates_AndRedraws()
    {
        var shell = new AShell
        {
            Timelines = FakeTimelineReader.Holding(APost.With(id: "1", account: "mark@a.social", author: "Mark")),
            Accounts = FakeAccountRelationships.Holding(null, Capped()),
            Search = FakeInstanceSearch.Finding(
                accounts: [Account("maria@b.social", "Maria"), Account("mark@a.social", "Mark")]),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("ma");
        shell.Host.Drain();

        var redrawn = 0;

        opened.Changed += () => redrawn++;
        shell.Host.Settle();

        Assert.Equal(["mark@a.social", "maria@b.social"], Addresses(opened, "ma"));
        Assert.True(redrawn > 0);
    }

    /// <summary>A query searched once is remembered for the session, and asked again only of what came back.</summary>
    [Fact]
    public async Task TheSameQueryLaterIsAnsweredFromMemory()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(null, Capped()),
            Search = FakeInstanceSearch.Finding(accounts: [Account("maria@b.social", "Maria")]),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("ma");
        shell.Host.Settle();
        opened.PeopleMatching("m");
        shell.Host.Settle();
        opened.Back();
        opened.Compose();
        opened.PeopleMatching("ma");
        shell.Host.SettleAll();

        Assert.Equal(["ma", "m"], shell.Search.FollowedSearches.Select(searched => searched.Query));
        Assert.Equal(["maria@b.social"], Addresses(opened, "ma"));
    }

    /// <summary>The list asking again for the query it already asked for, as a redraw does, is not a new pause.</summary>
    [Fact]
    public async Task AskingAgainForTheSameQueryIsOnePause()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(null, Capped()),
            Search = FakeInstanceSearch.Finding(accounts: [Account("maria@b.social", "Maria")]),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("ma");
        shell.Host.Drain();
        opened.PeopleMatching("ma");
        opened.PeopleMatching("ma");
        shell.Host.SettleAll();

        Assert.Single(shell.Search.FollowedSearches);
    }

    /// <summary>A refused search leaves the list as it was, and says nothing.</summary>
    [Fact]
    public async Task ARefusedSearchLeavesTheListAsItWas_Silently()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.StillListing(Account("mabel@c.social", "Mabel")),
            Search = FakeInstanceSearch.RateLimited(),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("ma");
        shell.Host.Settle();

        Assert.Single(shell.Search.FollowedSearches);
        Assert.Equal(["mabel@c.social"], Addresses(opened, "ma"));
        Assert.Null(opened.Notice);
        Assert.IsType<ComposeScreen>(opened.Screen);
    }

    /// <summary>The search goes out as the profile acted as, and counts as a request like any other.</summary>
    [Fact]
    public async Task TheSearchIsCountedAmongTheRequests()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.StillListing(),
            Search = FakeInstanceSearch.FindingNothing(),
        };

        var opened = await Composing(shell);

        opened.PeopleMatching("ma");
        shell.Host.Drain();

        var before = shell.Requests;

        shell.Host.Settle();

        Assert.Equal(before + 1, shell.Requests);
    }

    private static async Task<Shell> Composing(AShell shell)
    {
        var opened = await shell.Opened();

        opened.Compose();

        return opened;
    }

    /// <summary>A follow list as long as the read is allowed to be, none of whom answers to <c>m</c>.</summary>
    private static Account[] Capped() =>
    [
        .. Enumerable.Range(0, PeopleToMention.FollowsRead).Select(at => Account($"z{at}@z.social", $"Zed {at}")),
    ];

    private static Account Account(string address, string author) =>
        AnAccount.With(id: address, address: address, author: author);

    private static IReadOnlyList<string> Addresses(Shell shell, string query) =>
        [.. shell.PeopleMatching(query).Select(person => person.Address)];
}
