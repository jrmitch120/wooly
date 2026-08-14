using Wooly.Tests.Fakes;
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

    [Theory]
    [InlineData(ShellKey.Author, true)]
    [InlineData(ShellKey.Reject, false)]
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

        await opened.Press(key);
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

        await opened.Press(ShellKey.Enter);
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

        var drawn = opened.Screen.Lines(61, AShell.Now).Select(line => line.Text);

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
}
