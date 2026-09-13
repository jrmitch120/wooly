using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     The follow-requests screen: who is waiting to be let in, and what <c>a</c> and <c>x</c> answer. A request is
///     named by the id of the account that asked (ADR-0012), which is what these prove is what goes out.
/// </summary>
public class ShellRequestTests
{
    /// <summary>Where the rail's follow-requests destination is, counting from Home.</summary>
    private const int ToRequests = 6;

    [Fact]
    public async Task Step_ListsWhoIsWaitingToBeLetIn()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(
                null,
                AnAccount.With(id: "42", address: "alice@hachyderm.io"),
                AnAccount.With(id: "43", address: "bob@mastodon.social")),
        };

        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        var screen = Assert.IsType<FollowRequestsScreen>(opened.Screen);
        Assert.Equal(["42", "43"], screen.Waiting.Select(account => account.Id));
        Assert.Equal("follow requests", opened.Breadcrumb);
        Assert.Equal(2, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Requests).Unread);
    }

    /// <summary>
    ///     Somebody waiting is drawn as the Account block every listing draws — which is where this screen gains the
    ///     joined month and the flags, <c>⚿ locked</c> arguably being the most useful glyph on it (#198).
    /// </summary>
    [Fact]
    public async Task Step_DrawsWhoIsWaitingAsTheSharedAccountBlock()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(
                null,
                AnAccount.With(id: "42", address: "alice@hachyderm.io", author: "Alice", isLocked: true)),
        };

        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        // Past the one column the gutter takes, which every list on this shell stamps and no screen draws itself.
        Assert.Equal(
            [
                "Alice",
                "@alice@hachyderm.io",
                "4,210 posts · 187 following · 1,203 followers",
                "Joined Jan 2020 · ⚿ locked",
            ],
            opened.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text[1..]));
    }

    /// <summary>
    ///     A blank stands between two of them and no rule does: four-row blocks laid end to end run together, and a
    ///     blank costs the one row a rule would while leaving the list looking like the list of people it is.
    /// </summary>
    [Fact]
    public async Task Step_PutsOneBlankBetweenPeopleAndDrawsNoRules()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(
                null,
                AnAccount.With(id: "42", address: "alice@hachyderm.io"),
                AnAccount.With(id: "43", address: "bob@mastodon.social")),
        };

        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        var rows = opened.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text).ToList();

        Assert.Equal(9, rows.Count);
        Assert.Equal(string.Empty, rows[4].Trim());
        Assert.DoesNotContain(rows, row => row.Contains('─', StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ShellKey.A, true)]
    [InlineData(ShellKey.X, false)]
    public async Task Press_AnswersThePickedRequestByTheIdOfTheAccountThatAsked(ShellKey key, bool accepted)
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(
                null,
                AnAccount.With(id: "42", address: "alice@hachyderm.io"),
                AnAccount.With(id: "43", address: "bob@mastodon.social")),
        };

        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        opened.Press(key);
        shell.Host.Drain();

        var answered = Assert.Single(shell.Accounts.Answers);
        Assert.Equal("42", answered.AccountId);
        Assert.Equal(accepted, answered.Accepted);

        // Answered is answered: they leave the list, and the badge follows.
        var screen = Assert.IsType<FollowRequestsScreen>(opened.Screen);
        Assert.Equal(["43"], screen.Waiting.Select(account => account.Id));
        Assert.Equal(1, opened.Rail.Destinations.First(place => place.Kind == DestinationKind.Requests).Unread);
        Assert.Contains("alice@hachyderm.io", opened.Notice);
    }

    /// <summary>Answering a request is a decision about a person, so their account is one keypress away.</summary>
    [Fact]
    public async Task Press_OpensTheAccountOfWhoeverIsAsking()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(
                AnAccount.With(address: "alice@hachyderm.io"),
                AnAccount.With(id: "42", address: "alice@hachyderm.io")),
        };

        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        var account = Assert.IsType<AccountScreen>(opened.Screen);
        Assert.Equal("alice@hachyderm.io", account.Account.Address);

        // Pushed rather than arrived at, so esc comes back to the list still to be answered.
        Assert.Equal(2, opened.Depth);
    }

    /// <summary>
    ///     Only a locked account ever has any of these, so an empty list is the ordinary case and says so rather than
    ///     drawing nothing at all.
    /// </summary>
    [Fact]
    public async Task Step_SaysSoWhereNobodyIsWaiting()
    {
        var shell = new AShell { Accounts = FakeAccountRelationships.HoldingNobody() };
        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        var drawn = opened.Screen.Lines(new Drawing(61, AShell.Now)).Select(line => line.Text);

        Assert.Contains(drawn, line => line.Contains("Nobody is waiting", StringComparison.Ordinal));
    }

    /// <summary>Nobody picked out is nothing to answer, rather than an answer about somebody who is not there.</summary>
    [Fact]
    public async Task AnswerRequest_AnswersNothingWhereNobodyIsWaiting()
    {
        var shell = new AShell { Accounts = FakeAccountRelationships.HoldingNobody() };
        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        await opened.AnswerRequest(accepted: true);

        Assert.Empty(shell.Accounts.Answers);
    }

    /// <summary>
    ///     <c>a</c> and <c>x</c> collide with the keys that open an author and show a warning, which is workable only
    ///     because the status row says which is on offer (<c>docs/tui-shell.md</c>).
    /// </summary>
    [Fact]
    public async Task Keys_SayThatAAcceptsAndXRejectsHere()
    {
        var shell = new AShell();
        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        Assert.Contains(opened.Keys, key => key is { Key: "a", Does: "accept" });
        Assert.Contains(opened.Keys, key => key is { Key: "x", Does: "reject" });
    }

    /// <summary>
    ///     Everybody waiting is asked about at once, in the one call that endpoint takes many ids for — so a row can
    ///     say whether you already follow the person asking to follow you, which may be the most useful thing on it
    ///     (#204).
    /// </summary>
    [Fact]
    public async Task Step_AsksWhereTheReaderStandsWithEverybodyWaitingAtOnce()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(
                null,
                AnAccount.With(id: "42", address: "alice@hachyderm.io", author: "Alice"),
                AnAccount.With(id: "43", address: "bob@mastodon.social")),
        };

        shell.Accounts.Stands = AnAccount.Standing(following: true);

        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        var stood = Assert.Single(shell.Accounts.Standings);
        Assert.Equal(["42", "43"], stood.AccountIds);

        Assert.Contains("Joined Jan 2020 · following", AShell.Drawn(opened.Screen));
    }

    /// <summary>
    ///     A list with nobody on it is nothing to decorate, so nothing reaches the instance — the port answers an
    ///     empty ask with its own input before it creates a client, which is the gate
    ///     <c>AccountRelationshipsTests.Standing_AsksNothingOfTheInstanceForAnEmptyList</c> holds against real HTTP.
    /// </summary>
    [Fact]
    public async Task Step_AsksForNoStandingWhereNobodyIsWaiting()
    {
        var shell = new AShell { Accounts = FakeAccountRelationships.HoldingNobody() };
        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        Assert.Empty(shell.Accounts.Standings);
    }

    /// <summary>
    ///     A refresh re-asks for free, running the same read the arrival did — which is what putting the standing
    ///     call inside the destination's own read buys (#204).
    /// </summary>
    [Fact]
    public async Task Refresh_AsksWhereTheReaderStandsAgain()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(null, AnAccount.With(id: "42", address: "alice@hachyderm.io")),
        };

        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        await opened.Refresh();
        shell.Host.Settle();

        Assert.Equal(2, shell.Accounts.Standings.Count);
    }

    /// <summary>
    ///     A standing the instance never answered leaves the rows silent and the list standing: no suffix saying there
    ///     is no tie, and no notice about a call nobody asked for.
    /// </summary>
    [Fact]
    public async Task Step_DrawsSilentRowsWhereTheStandingWasNeverAnswered()
    {
        var shell = new AShell
        {
            Accounts = FakeAccountRelationships.Holding(
                null,
                AnAccount.With(id: "42", address: "alice@hachyderm.io", author: "Alice")),
        };

        shell.Accounts.Stands = null;

        var opened = await shell.Opened();

        opened.Step(ToRequests);
        shell.Host.Settle();

        var screen = Assert.IsType<FollowRequestsScreen>(opened.Screen);

        Assert.Equal("42", Assert.Single(screen.Waiting).Id);
        Assert.Contains("Joined Jan 2020", AShell.Drawn(opened.Screen));
        Assert.Null(screen.Notice);
        Assert.Null(opened.Notice);
    }

    /// <summary>
    ///     What this list implies is a <em>negative</em> — "they follow you" is false by definition of a pending
    ///     request — and the compact standing only ever adds words for positives, so a follow already in place shows
    ///     and somebody with no tie draws nothing extra.
    /// </summary>
    /// <remarks>
    ///     Pinned here rather than left to the type, so that a compact standing which one day learns to say a negative
    ///     cannot start telling this screen's reader they are not followed by somebody who is asking to follow them.
    /// </remarks>
    [Fact]
    public void Lines_SaysAFollowInPlaceAndNothingAtAllWhereThereIsNoTie()
    {
        var screen = new FollowRequestsScreen([
            AnAccount.With(id: "42", address: "alice@hachyderm.io", standing: AnAccount.Standing(following: true)),
            AnAccount.With(id: "43", address: "bob@mastodon.social", standing: AnAccount.Standing()),
        ]);

        var rows = AShell.Drawn(screen);

        Assert.Contains("Joined Jan 2020 · following", rows);
        Assert.Contains("Joined Jan 2020", rows);
    }

}
