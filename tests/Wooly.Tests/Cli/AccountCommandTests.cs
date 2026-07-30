using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Accounts;
using Wooly.Core.Credentials;
using Wooly.Core.Errors;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Managing relationships the way a user manages them: whole commands through the real command app, over a real
///     config file and token store in a scratch directory, with the instance faked at
///     <see cref="IAccountRelationships" /> — ADR-0005's primary seam, which is what a command test is meant to fake.
/// </summary>
public class AccountCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakeAccountRelationships _relationships = FakeAccountRelationships.Holding();

    public void Dispose() => _directory.Dispose();

    /// <summary>
    ///     Three ties that are on or off, not six acts (ADR-0009's shape): every verb reaches the same port with the
    ///     tie it means and whether it is putting it on or taking it off.
    /// </summary>
    [Theory]
    [InlineData("follow", AccountTie.Follow, true)]
    [InlineData("unfollow", AccountTie.Follow, false)]
    [InlineData("block", AccountTie.Block, true)]
    [InlineData("unblock", AccountTie.Block, false)]
    [InlineData("mute", AccountTie.Mute, true)]
    [InlineData("unmute", AccountTie.Mute, false)]
    public void Account_PutsTheTieTheVerbNamesOnTheAccountOrTakesItOff(string verb, AccountTie tie, bool wanted)
    {
        AddProfile();

        var run = Run(["account", verb, "alice@hachyderm.io"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var tied = Assert.Single(_relationships.Ties);
        Assert.Equal(tie, tied.Tie);
        Assert.Equal(wanted, tied.Wanted);
        Assert.Equal("alice@hachyderm.io", tied.Account.Text);
        Assert.Equal("personal", tied.Profile);
    }

    /// <summary>
    ///     One table of words, so six commands cannot come to describe three ties in more than six ways — and each
    ///     names the account, which is what the user typed and what they will type next.
    /// </summary>
    [Theory]
    [InlineData("follow", "Now following")]
    [InlineData("unfollow", "Unfollowed")]
    [InlineData("block", "Blocked")]
    [InlineData("unblock", "Unblocked")]
    [InlineData("mute", "Muted")]
    [InlineData("unmute", "Unmuted")]
    public void Account_SaysWhatItDid(string verb, string said)
    {
        AddProfile();

        var run = Run(["account", verb, "alice@hachyderm.io"]);

        Assert.Contains($"{said} alice@hachyderm.io", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());
    }

    /// <summary>
    ///     Following a locked account leaves a request behind rather than a follow. Saying "now following" over one
    ///     nobody has accepted would tell the user their timeline is about to change when it is not.
    /// </summary>
    [Fact]
    public void Account_SaysAFollowWaitingOnALockedAccountIsAskedForRatherThanInPlace()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(
            AnAccount.With(standing: AnAccount.Standing(followRequested: true)));

        var run = Run(["account", "follow", "alice@hachyderm.io"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("Asked to follow alice@hachyderm.io", run.Output);
        Assert.DoesNotContain("Now following", run.Output);
    }

    /// <summary>
    ///     The same command withdraws a follow request that was never accepted, and what comes back cannot tell that
    ///     from an ordinary unfollow — so it says only that it was done, rather than claiming a follow that may never
    ///     have existed.
    /// </summary>
    [Fact]
    public void Account_ClaimsNoFollowItCannotKnowThereWasWhenUnfollowing()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(AnAccount.With(standing: AnAccount.Standing()));

        var run = Run(["account", "unfollow", "alice@hachyderm.io"]);

        Assert.Contains("Unfollowed alice@hachyderm.io", run.Output);
        Assert.DoesNotContain("No longer following", run.Output);
    }

    /// <summary>A bare username is somebody on the profile's own instance, which is how that instance lists them.</summary>
    [Fact]
    public void Account_TakesAnAddressHoweverMastodonWouldHaveShownIt()
    {
        AddProfile();

        Assert.Equal((int)ExitCode.Success, Run(["account", "follow", "@alice@hachyderm.io"]).ExitCode);
        Assert.Equal((int)ExitCode.Success, Run(["account", "follow", "bob"]).ExitCode);

        Assert.Equal(["alice@hachyderm.io", "bob"], _relationships.Ties.Select(tie => tie.Account.Text));
    }

    /// <summary>
    ///     Turned down against the value the user typed, rather than as a failure that looks like the instance's fault
    ///     — and before anything is asked of the instance at all.
    /// </summary>
    [Theory]
    [InlineData("alice@")]
    [InlineData("alice bob")]
    public void Account_ReportsAnAddressThatNamesNoAccountAsAUsageError(string address)
    {
        AddProfile();

        var run = Run(["account", "block", address]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("user@instance", run.ErrorOutput);
        Assert.Empty(_relationships.Ties);
    }

    /// <summary>An account the instance cannot find is a value on the command line that is wrong, not a broken client.</summary>
    [Fact]
    public void Account_ReportsAnAccountTheInstanceCouldNotFindAsAUsageError()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Refusing(
            new UnknownAccountException(AccountAddress.Parse("nobody@hachyderm.io"), "mastodon.social"));

        var run = Run(["account", "follow", "nobody@hachyderm.io"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("could not find an account called nobody@hachyderm.io", run.ErrorOutput);
    }

    /// <summary>The one account a user never has to name is their own, which is whose list they meant.</summary>
    [Theory]
    [InlineData("followers", FollowSide.Followers)]
    [InlineData("following", FollowSide.Following)]
    public void Account_ListsTheProfilesOwnFollowsWhenNobodyIsNamed(string verb, FollowSide side)
    {
        AddProfile();

        var run = Run(["account", verb]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var listed = Assert.Single(_relationships.Lists);
        Assert.Equal(side, listed.Side);
        Assert.Null(listed.Account);
        Assert.Contains("alice@hachyderm.io", run.Output);
    }

    [Theory]
    [InlineData("followers", FollowSide.Followers)]
    [InlineData("following", FollowSide.Following)]
    public void Account_ListsTheFollowsOfTheAccountThatWasNamed(string verb, FollowSide side)
    {
        AddProfile();

        var run = Run(["account", verb, "bob@hachyderm.io"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var listed = Assert.Single(_relationships.Lists);
        Assert.Equal(side, listed.Side);
        Assert.Equal("bob@hachyderm.io", listed.Account?.Text);
    }

    /// <summary>An account is worth listing for what it is: who it is, and how much of a presence it has.</summary>
    [Fact]
    public void Account_ShowsWhoEachAccountIsTheWayASearchShowsThem()
    {
        AddProfile();

        var run = Run(["account", "followers"]);

        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.Contains("Alice", run.Output);
        Assert.Contains("1203 followers", run.Output);
        Assert.Contains("4210 posts", run.Output);
        Assert.Contains("https://hachyderm.io/@alice", run.Output);
    }

    /// <summary>
    ///     Printing nothing at all leaves a user unable to tell an empty list from a broken client — and an empty one
    ///     says whose it was, since read on its own "no followers" could as easily be about the account that was named
    ///     as about the profile that asked.
    /// </summary>
    [Fact]
    public void Account_SaysSoWhenNobodyIsOnTheList()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.HoldingNobody();

        var run = Run(["account", "followers"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("Nobody follows jeff@mastodon.social.", run.Output);
    }

    [Fact]
    public void Account_SaysWhoseListWasEmptyWhenSomebodyElseWasNamed()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.HoldingNobody();

        var run = Run(["account", "following", "bob@hachyderm.io"]);

        Assert.Contains("bob@hachyderm.io follows nobody.", run.Output);
    }

    [Fact]
    public void Account_AsksForAsManyAccountsAsTheLimitSays()
    {
        AddProfile();

        var run = Run(["account", "followers", "--limit", "60"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(60, Assert.Single(_relationships.Lists).Limit);
    }

    [Fact]
    public void Account_ReportsALimitOfNoAccountsAsAUsageError()
    {
        AddProfile();

        var run = Run(["account", "followers", "--limit", "0"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_relationships.Lists);
    }

    /// <summary>
    ///     What arrived is worth having, so it is shown before the limit that stopped the rest is reported — the same
    ///     bargain a timeline read makes (ADR-0007).
    /// </summary>
    [Fact]
    public void Account_ShowsWhatArrivedBeforeReportingTheRateLimitThatStoppedTheRest()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.RateLimitedAfter(AnAccount.With());

        var run = Run(["account", "followers"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);
    }

    /// <summary>The id leads, because it is the one thing on the line the next command asks the user to type.</summary>
    [Fact]
    public void Account_ListsThePendingRequestsWithTheIdThatAnswersThem()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(listing: [AnAccount.With(id: "42")]);

        var run = Run(["account", "requests", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("42", run.Output);
        Assert.Contains("alice@hachyderm.io", run.Output);

        var listed = Assert.Single(_relationships.Lists);
        Assert.Null(listed.Side);
    }

    [Fact]
    public void Account_SaysSoWhenNoFollowRequestsAreWaiting()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.HoldingNobody();

        var run = Run(["account", "requests", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("No follow requests waiting.", run.Output);
    }

    [Theory]
    [InlineData("accept", true, "Accepted the follow request from")]
    [InlineData("reject", false, "Rejected the follow request from")]
    public void Account_AnswersAPendingRequestAndSaysWhoItWasFrom(string verb, bool accepted, string said)
    {
        AddProfile();

        var run = Run(["account", "requests", verb, "42"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var answered = Assert.Single(_relationships.Answers);
        Assert.Equal("42", answered.AccountId);
        Assert.Equal(accepted, answered.Accepted);
        Assert.Contains($"{said} alice@hachyderm.io", run.Output);
    }

    [Fact]
    public void Account_ReportsAMissingRequestIdAsAUsageError()
    {
        AddProfile();

        var run = Run(["account", "requests", "accept"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_relationships.Answers);
    }

    [Fact]
    public void Account_ActsAsTheProfileNamedByTheOverrideWithoutChangingTheDefault()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        var run = Run(["account", "follow", "alice@hachyderm.io", "--profile", "work"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("work", Assert.Single(_relationships.Ties).Profile);
    }

    [Fact]
    public void Account_ReportsThatNothingIsSetUpYetWithTheAuthenticationExitCode()
    {
        var run = Run(["account", "followers"]);

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);
        Assert.Contains("No profiles", run.ErrorOutput);
        Assert.Empty(_relationships.Lists);
    }

    /// <summary>
    ///     The standing is nested rather than spread across the top level, because <c>following</c> already means how
    ///     many accounts this one follows — one field cannot be both a count and a yes-or-no.
    /// </summary>
    [Fact]
    public void Account_WritesWhereTheProfileStandsAsMachineReadableJson()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(
            AnAccount.With(standing: AnAccount.Standing(following: true, followedBy: true)));

        var run = Run(["account", "follow", "alice@hachyderm.io", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var account = JsonDocument.Parse(run.Output).RootElement;
        Assert.Equal("42", account.GetProperty("id").GetString());
        Assert.Equal("alice@hachyderm.io", account.GetProperty("account").GetString());
        Assert.Equal(187, account.GetProperty("following").GetInt64());

        var standing = account.GetProperty("standing");
        Assert.True(standing.GetProperty("following").GetBoolean());
        Assert.True(standing.GetProperty("followedBy").GetBoolean());
        Assert.False(standing.GetProperty("blocking").GetBoolean());
    }

    /// <summary>
    ///     A list is an object rather than a bare array, per ADR-0007: cut short by a rate limit and holding nobody are
    ///     otherwise the same two characters.
    /// </summary>
    [Fact]
    public void Account_WritesAListAsMachineReadableJsonSayingWhoseItIsAndWhetherItIsAllOfIt()
    {
        AddProfile();

        var run = Run(["account", "followers", "bob@hachyderm.io", "--json"]);

        var document = JsonDocument.Parse(run.Output).RootElement;
        Assert.Equal("followers", document.GetProperty("list").GetString());
        Assert.Equal("bob@hachyderm.io", document.GetProperty("account").GetString());
        Assert.True(document.GetProperty("complete").GetBoolean());

        var account = Assert.Single(document.GetProperty("accounts").EnumerateArray().ToList());
        Assert.Equal("alice@hachyderm.io", account.GetProperty("account").GetString());

        // Mastodon says nothing about standing in a followers list, and five falses would say the profile follows
        // none of the accounts it is reading.
        Assert.False(account.TryGetProperty("standing", out _));
    }

    [Fact]
    public void Account_WritesAListStoppedByARateLimitAsIncomplete()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.RateLimitedAfter(AnAccount.With());

        var run = Run(["account", "followers", "--json"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);

        var document = JsonDocument.Parse(run.Output).RootElement;
        Assert.False(document.GetProperty("complete").GetBoolean());
        Assert.Equal("mastodon.social", document.GetProperty("rateLimit").GetProperty("instance").GetString());
    }

    [Fact]
    public void Account_WritesThePendingRequestsAsMachineReadableJson()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(listing: [AnAccount.With(id: "42")]);

        var run = Run(["account", "requests", "list", "--json"]);

        var document = JsonDocument.Parse(run.Output).RootElement;
        Assert.Equal("requests", document.GetProperty("list").GetString());

        var account = Assert.Single(document.GetProperty("accounts").EnumerateArray().ToList());
        Assert.Equal("42", account.GetProperty("id").GetString());
    }

    /// <summary>CONTEXT.md's vocabulary, at the one place a user reads it: an instance is not a server.</summary>
    [Fact]
    public void Account_NamesWhatItShowsInThisProjectsVocabulary()
    {
        AddProfile();

        var run = Run(["account", "followers"]);

        Assert.DoesNotContain("server", run.Output);
        Assert.DoesNotContain("toot", run.Output);
        Assert.DoesNotContain("status", run.Output);
    }

    private void AddProfile(string name = "personal", string instance = "mastodon.social") =>
        Run(["profile", "add", name, "--instance", instance, "--token", $"token-{name}"]);

    private CommandRun Run(string[] args, int consoleWidth = 200)
    {
        var console = new TestConsole().Width(consoleWidth);
        var errorConsole = new TestConsole().Width(consoleWidth);

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton(new WoolyPaths(_directory.Path));
            services.AddSingleton<ICredentialStore>(new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
            services.AddSingleton<IAccessTokenVerifier>(FakeAccessTokenVerifier.Accepting());
            services.AddSingleton<IAccountRelationships>(_relationships);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
