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
    /// <summary>When the instance proved the link on a custom field, for the tests that set one.</summary>
    private static readonly DateTimeOffset Proved = new(2024, 3, 2, 9, 0, 0, TimeSpan.Zero);

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
        Assert.Equal("bob@hachyderm.io", listed.Account?.Address.Text);
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

    /// <summary>
    ///     The command the <c>account</c> branch turns out never to have had: who somebody is, read on its own.
    ///     <c>profile show</c> is the local credential entry and a different thing entirely (CONTEXT.md).
    /// </summary>
    [Fact]
    public void Account_ShowsWhoAnAccountIsWhenOneIsNamed()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(
            AnAccount.With(standing: AnAccount.Standing(following: true, followedBy: true)));

        var run = Run(["account", "show", "alice@hachyderm.io"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var read = Assert.Single(_relationships.Reads);
        Assert.Equal("alice@hachyderm.io", read.Account.Text);

        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.Contains("Alice", run.Output);
        Assert.Contains("1203 followers", run.Output);
        Assert.Contains("Cat photographer.", run.Output);
        Assert.Contains("Joined Jan 2020", run.Output);

        Assert.Contains("following, follows you", run.Output);
        Assert.Contains("https://hachyderm.io/@alice", run.Output);
    }

    /// <summary>
    ///     Familiar followers stays off the CLI — a call spent to say something only a screen benefits from — and the
    ///     read itself stays the one call it always was (ADR-0019).
    /// </summary>
    [Fact]
    public void Account_AsksTheInstanceNothingBeyondTheOneReadWhenShowingAnAccount()
    {
        AddProfile();

        Assert.Equal((int)ExitCode.Success, Run(["account", "show", "alice@hachyderm.io"]).ExitCode);

        Assert.Single(_relationships.Reads);
        Assert.Empty(_relationships.Familiars);
        Assert.Empty(_relationships.Lists);
        Assert.Empty(_relationships.Standings);
    }

    /// <summary>
    ///     A verified field is marked after its value; one nobody proved says nothing extra, absence being the honest
    ///     signal since a field nobody proved is not a field anybody disproved (CONTEXT.md).
    /// </summary>
    [Fact]
    public void Account_ShowsTheFieldsAnAccountSetAndMarksOnlyTheOnesItsInstanceProved()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(AnAccount.With(fields:
        [
            AnAccount.Field("Website", "https://alice.example", Proved),
            AnAccount.Field("Pronouns", "she/her"),
        ]));

        var run = Run(["account", "show", "alice@hachyderm.io"]);

        Assert.Contains("Website: https://alice.example ✓", run.Output);
        Assert.Contains("Pronouns: she/her", run.Output);
        Assert.DoesNotContain("she/her ✓", run.Output);
    }

    /// <summary>A flag nobody set says nothing rather than saying no.</summary>
    [Fact]
    public void Account_SaysWhichFlagsAnAccountCarriesAndNothingOfTheOnesItDoesNot()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(AnAccount.With(isBot: true));

        var run = Run(["account", "show", "alice@hachyderm.io"]);

        Assert.Contains("bot", run.Output);
        Assert.DoesNotContain("locked", run.Output);
    }

    /// <summary>
    ///     The reader's own words about somebody else, which nobody else ever sees and which arrives on the standing
    ///     this read already asked for (CONTEXT.md).
    /// </summary>
    [Fact]
    public void Account_ShowsTheNoteTheProfileKeptAboutAnAccount()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(
            AnAccount.With(standing: AnAccount.Standing(note: "Met at a conference.")));

        var run = Run(["account", "show", "alice@hachyderm.io"]);

        Assert.Contains("Your note: Met at a conference.", run.Output);
    }

    /// <summary>
    ///     An account that is nothing to the profile is worth saying here, unlike on a list: this is the one command
    ///     asked where the profile stands, and a silence would read as a line that failed to print.
    /// </summary>
    [Fact]
    public void Account_SaysWhenThereAreNoTiesEitherWay()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(AnAccount.With(standing: AnAccount.Standing()));

        var run = Run(["account", "show", "alice@hachyderm.io"]);

        Assert.Contains("No ties either way.", run.Output);
    }

    /// <summary>An address that names nobody is a value on the command line that is wrong, not a broken client.</summary>
    [Fact]
    public void Account_ReportsAnAccountItCouldNotShowAsAUsageError()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Refusing(
            new UnknownAccountException(AccountAddress.Parse("nobody@hachyderm.io"), "mastodon.social"));

        var run = Run(["account", "show", "nobody@hachyderm.io"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("could not find an account called nobody@hachyderm.io", run.ErrorOutput);
        Assert.Empty(run.Output.Trim());
    }

    /// <summary>Turned down before anything is asked of the instance at all, as every other account command does.</summary>
    [Fact]
    public void Account_ReportsAnAddressThatShowCannotParseAsAUsageError()
    {
        AddProfile();

        var run = Run(["account", "show", "alice@"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Contains("user@instance", run.ErrorOutput);
        Assert.Empty(_relationships.Reads);
    }

    /// <summary>
    ///     A bio is the account's own writing, and a square bracket in one is not a colour tag — the rule this whole
    ///     writer works under, at the one place a stranger's text reaches it.
    /// </summary>
    [Fact]
    public void Account_PrintsABioAsTheAccountWroteItRatherThanAsMarkup()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(AnAccount.With(bio: "Reading [bold]everything[/]."));

        var run = Run(["account", "show", "alice@hachyderm.io"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("Reading [bold]everything[/].", run.Output);
    }

    /// <summary>
    ///     A bio arrives with a blank line between its paragraphs, and that blank is written blank — an indent nobody
    ///     can see is still two characters for whatever reads the output back.
    /// </summary>
    [Fact]
    public void Account_LeavesNothingOnTheBlankRowBetweenABiosParagraphs()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(AnAccount.With(bio: "Cat photographer.\n\nAsk me about film."));

        var run = Run(["account", "show", "alice@hachyderm.io"]);

        Assert.Contains("  Cat photographer.\n\n  Ask me about film.", run.Output);
    }

    /// <summary>
    ///     A followers list does not print forty bios: <c>account show</c> got its own fuller writer precisely so the
    ///     list's report could stay as it was (ADR-0019).
    /// </summary>
    [Fact]
    public void Account_PrintsNoBiosDownAFollowersList()
    {
        AddProfile();

        var run = Run(["account", "followers"]);

        Assert.DoesNotContain("Cat photographer.", run.Output);
        Assert.DoesNotContain("Joined", run.Output);
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
    ///     One spelling for an account everywhere (ADR-0011) is what puts a bio within reach of a pipe: everything the
    ///     report says is here too, with the standing nested as ADR-0012 requires.
    /// </summary>
    [Fact]
    public void Account_WritesEverythingItShowsAsMachineReadableJson()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(AnAccount.With(
            standing: AnAccount.Standing(following: true, note: "Met at a conference."),
            fields: [AnAccount.Field("Website", "https://alice.example", Proved)],
            joined: new DateOnly(2019, 6, 4),
            isLocked: true,
            isBot: true,
            avatarUrl: "https://hachyderm.io/avatars/alice.png"));

        var run = Run(["account", "show", "alice@hachyderm.io", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var account = JsonDocument.Parse(run.Output).RootElement;
        Assert.Equal("alice@hachyderm.io", account.GetProperty("account").GetString());
        Assert.Equal("Cat photographer.", account.GetProperty("bio").GetString());
        Assert.Equal("2019-06-04", account.GetProperty("joined").GetString());
        Assert.True(account.GetProperty("locked").GetBoolean());
        Assert.True(account.GetProperty("bot").GetBoolean());
        Assert.Equal("https://hachyderm.io/avatars/alice.png", account.GetProperty("avatar").GetString());

        var field = Assert.Single(account.GetProperty("fields").EnumerateArray().ToList());
        Assert.Equal("Website", field.GetProperty("label").GetString());
        Assert.Equal("https://alice.example", field.GetProperty("said").GetString());
        Assert.Equal(Proved, field.GetProperty("verified").GetDateTimeOffset());

        var standing = account.GetProperty("standing");
        Assert.True(standing.GetProperty("following").GetBoolean());
        Assert.Equal("Met at a conference.", standing.GetProperty("note").GetString());
    }

    /// <summary>
    ///     A field nobody proved carries no moment at all rather than one that means nothing, and a profile that wrote
    ///     itself no note carries none — the same rule the rest of this output follows.
    /// </summary>
    [Fact]
    public void Account_LeavesOutTheProofAndTheNoteThatWereNeverThere()
    {
        AddProfile();
        _relationships = FakeAccountRelationships.Holding(AnAccount.With(
            standing: AnAccount.Standing(),
            fields: [AnAccount.Field("Pronouns", "she/her")]));

        var run = Run(["account", "show", "alice@hachyderm.io", "--json"]);

        var account = JsonDocument.Parse(run.Output).RootElement;
        var field = Assert.Single(account.GetProperty("fields").EnumerateArray().ToList());

        Assert.False(field.TryGetProperty("verified", out _));
        Assert.False(account.GetProperty("standing").TryGetProperty("note", out _));
    }

    /// <summary>
    ///     The same account fields wherever an account turns up, so a script needs one filter rather than one per
    ///     command that turned an account up — and noise is cheap in a pipe (ADR-0011).
    /// </summary>
    [Fact]
    public void Account_CarriesTheSameAccountFieldsInAListsJson()
    {
        AddProfile();

        var run = Run(["account", "followers", "--json"]);

        var document = JsonDocument.Parse(run.Output).RootElement;
        var account = Assert.Single(document.GetProperty("accounts").EnumerateArray().ToList());

        Assert.Equal("Cat photographer.", account.GetProperty("bio").GetString());
        Assert.Equal("2020-01-01", account.GetProperty("joined").GetString());
        Assert.False(account.GetProperty("locked").GetBoolean());
        Assert.False(account.GetProperty("bot").GetBoolean());
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
