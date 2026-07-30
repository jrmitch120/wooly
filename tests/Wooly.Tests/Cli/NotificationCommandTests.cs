using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;
using Wooly.Cli;
using Wooly.Core;
using Wooly.Core.Credentials;
using Wooly.Core.Notifications;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;

namespace Wooly.Tests.Cli;

/// <summary>
///     Notifications driven the way a user drives them: whole commands through the real command app, over a real config
///     file and token store in a scratch directory, with the instance's inbox faked at <see cref="INotificationInbox" />
///     — ADR-0005's primary seam, which is what a command test is meant to fake.
/// </summary>
public class NotificationCommandTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private FakeNotificationInbox _inbox = FakeNotificationInbox.Holding(ANotification.With());

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void List_ShowsWhatIsWaitingForTheAccount()
    {
        AddProfile();

        var run = Run(["notification", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("alice@hachyderm.io", run.Output);
        Assert.Contains("mentioned you", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());

        var read = Assert.Single(_inbox.Reads);
        Assert.Equal("personal", read.Profile);
    }

    /// <summary>
    ///     The id is what <c>notification dismiss</c> takes, so a list that did not show it would leave a user with no
    ///     way to name the thing they just read.
    /// </summary>
    [Fact]
    public void List_ShowsTheIdEachNotificationIsDismissedBy()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.Holding(ANotification.With(id: "34"));

        var run = Run(["notification", "list"]);

        Assert.Contains("34", run.Output);
    }

    /// <summary>The four kinds #24 asks for, each said in a way that reads as a sentence about who did what.</summary>
    [Fact]
    public void List_SaysWhatEachOfTheFourKindsOfNotificationIs()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.Holding(
            ANotification.With(id: "34", kind: NotificationKind.Mention),
            ANotification.Follow(id: "35"),
            ANotification.With(id: "36", kind: NotificationKind.Boost),
            ANotification.With(id: "37", kind: NotificationKind.Favorite));

        var run = Run(["notification", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("mentioned you", run.Output);
        Assert.Contains("followed you", run.Output);
        Assert.Contains("boosted your post", run.Output);
        Assert.Contains("favorited your post", run.Output);
    }

    /// <summary>
    ///     A kind this client has no word for is still something the account was notified about, and is still worth
    ///     showing — with the instance's own word for it, rather than a guess at what it meant.
    /// </summary>
    [Fact]
    public void List_ShowsAKindItHasNoWordForRatherThanHidingIt()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.Holding(
            ANotification.With(id: "34", kind: NotificationKind.Reported("poll")));

        var run = Run(["notification", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("34", run.Output);
        Assert.Contains("poll", run.Output);
    }

    /// <summary>
    ///     A mention is only worth reading with the post in it, and it reads exactly as the same post does on a
    ///     timeline, because both ask <c>PostReport.Write</c> for it.
    /// </summary>
    [Fact]
    public void List_ShowsThePostANotificationIsAbout()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.Holding(
            ANotification.With(post: APost.With(content: "Hey there, jeff")));

        var run = Run(["notification", "list"]);

        Assert.Contains("Hey there, jeff", run.Output);
        Assert.Contains("3 boosts", run.Output);
    }

    /// <summary>A follow has no post behind it, and a blank block is not what "nothing to read" should look like.</summary>
    [Fact]
    public void List_ShowsAFollowAsOneLineWithNoPostUnderIt()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.Holding(ANotification.Follow());

        var run = Run(["notification", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("bob@mastodon.social", run.Output);
        Assert.Contains("followed you", run.Output);
        Assert.DoesNotContain("boosts", run.Output);
    }

    /// <summary>CONTEXT.md's vocabulary, at the one place a user reads it: nothing on screen says reblog or toot.</summary>
    [Fact]
    public void List_NamesWhatItShowsInThisProjectsVocabulary()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.Holding(
            ANotification.With(id: "36", kind: NotificationKind.Boost),
            ANotification.With(id: "37", kind: NotificationKind.Favorite));

        var run = Run(["notification", "list"]);

        Assert.Contains("boosted", run.Output);
        Assert.Contains("favorited", run.Output);
        Assert.DoesNotContain("reblog", run.Output);
        Assert.DoesNotContain("favourite", run.Output);
        Assert.DoesNotContain("toot", run.Output);
    }

    /// <summary>Printing nothing at all leaves a user unable to tell an empty inbox from a broken client.</summary>
    [Fact]
    public void List_SaysSoWhenNothingIsWaiting()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.Holding();

        var run = Run(["notification", "list"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Contains("No notifications", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());
    }

    [Fact]
    public void List_AsksForAsManyNotificationsAsTheLimitSays()
    {
        AddProfile();

        var run = Run(["notification", "list", "--limit", "60"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal(60, Assert.Single(_inbox.Reads).Limit);
    }

    [Fact]
    public void List_AsksForAScreensWorthWhenNoLimitIsGiven()
    {
        AddProfile();

        var run = Run(["notification", "list"]);

        Assert.Equal(20, Assert.Single(_inbox.Reads).Limit);
    }

    [Fact]
    public void List_ReportsALimitOfNoNotificationsAsAUsageError()
    {
        AddProfile();

        var run = Run(["notification", "list", "--limit", "0"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_inbox.Reads);
    }

    [Fact]
    public void List_ReadsAsTheProfileNamedByTheOverrideWithoutChangingTheDefault()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        var run = Run(["notification", "list", "--profile", "work"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("work", Assert.Single(_inbox.Reads).Profile);
    }

    [Fact]
    public void List_ReportsThatNothingIsSetUpYetWithTheAuthenticationExitCode()
    {
        var run = Run(["notification", "list"]);

        Assert.Equal((int)ExitCode.AuthenticationError, run.ExitCode);
        Assert.Contains("No profiles", run.ErrorOutput);
        Assert.Empty(_inbox.Reads);
    }

    /// <summary>
    ///     ADR-0007 at the front end: what did arrive is printed and kept, the limit is reported as the failure it is,
    ///     and the exit code says which failure — so a script can tell this apart from an inbox that was simply empty.
    /// </summary>
    [Fact]
    public void List_ShowsWhatItGotAndReportsTheRateLimitThatStoppedTheRest()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.RateLimitedAfter(ANotification.With());

        var run = Run(["notification", "list"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.Contains("mentioned you", run.Output);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);
        Assert.DoesNotContain("Rate limited", run.Output);
    }

    /// <summary>
    ///     The case it is easiest to get wrong: a rate limit that stopped the fetch before anything arrived has nothing
    ///     to show, and must not therefore be described as an inbox with nothing in it.
    /// </summary>
    [Fact]
    public void List_DoesNotCallARateLimitedFetchAnEmptyInbox()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.RateLimitedAfter();

        var run = Run(["notification", "list"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);
        Assert.DoesNotContain("No notifications", run.Output);
        Assert.Contains("Rate limited by mastodon.social", run.ErrorOutput);
    }

    [Fact]
    public void List_WritesTheNotificationsAsMachineReadableJson()
    {
        AddProfile();

        var run = Run(["notification", "list", "--json"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);

        var inbox = JsonDocument.Parse(run.Output).RootElement;

        Assert.True(inbox.GetProperty("complete").GetBoolean());

        var notification = Assert.Single(inbox.GetProperty("notifications").EnumerateArray().ToList());
        Assert.Equal("34", notification.GetProperty("id").GetString());
        Assert.Equal("mention", notification.GetProperty("kind").GetString());
        Assert.Equal("alice@hachyderm.io", notification.GetProperty("account").GetString());
        Assert.Equal(
            new DateTimeOffset(2026, 7, 29, 12, 4, 0, TimeSpan.Zero),
            notification.GetProperty("receivedAt").GetDateTimeOffset());
        Assert.Equal("110", notification.GetProperty("post").GetProperty("id").GetString());
    }

    /// <summary>The post inside a notification is the same document every other command writes for a post.</summary>
    [Fact]
    public void List_WritesThePostInsideANotificationTheWayEveryOtherCommandWritesOne()
    {
        AddProfile();

        var run = Run(["notification", "list", "--json"]);

        var post = JsonDocument.Parse(run.Output)
                               .RootElement.GetProperty("notifications")[0]
                               .GetProperty("post");

        Assert.Equal("Hello world", post.GetProperty("content").GetString());
        Assert.Equal(3, post.GetProperty("boosts").GetInt64());
        Assert.Equal(5, post.GetProperty("favorites").GetInt64());
        Assert.Equal("public", post.GetProperty("visibility").GetString());
    }

    /// <summary>A follow carries no post, and a field that does not apply is left out rather than written as null.</summary>
    [Fact]
    public void List_LeavesThePostOutOfAFollowItWritesAsJson()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.Holding(ANotification.Follow());

        var run = Run(["notification", "list", "--json"]);

        var notification = JsonDocument.Parse(run.Output).RootElement.GetProperty("notifications")[0];

        Assert.Equal("follow", notification.GetProperty("kind").GetString());
        Assert.False(notification.TryGetProperty("post", out _));
    }

    /// <summary>
    ///     Under a pipe, a rate limit has to be readable from the output itself — the exit code is gone by the time the
    ///     JSON reaches whatever is parsing it, and an empty <c>notifications</c> would otherwise read as a quiet inbox.
    /// </summary>
    [Fact]
    public void List_MarksJsonIncompleteWhenARateLimitStoppedTheFetchShort()
    {
        AddProfile();
        _inbox = FakeNotificationInbox.RateLimitedAfter();

        var run = Run(["notification", "list", "--json"]);

        Assert.Equal((int)ExitCode.RateLimited, run.ExitCode);

        var inbox = JsonDocument.Parse(run.Output).RootElement;

        Assert.False(inbox.GetProperty("complete").GetBoolean());
        Assert.Empty(inbox.GetProperty("notifications").EnumerateArray().ToList());
        Assert.Equal("mastodon.social", inbox.GetProperty("rateLimit").GetProperty("instance").GetString());
        Assert.Equal(
            new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero),
            inbox.GetProperty("rateLimit").GetProperty("resetsAt").GetDateTimeOffset());
    }

    [Fact]
    public void Dismiss_ClearsTheOneNotificationItWasNamed()
    {
        AddProfile();

        var run = Run(["notification", "dismiss", "34"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("34", Assert.Single(_inbox.Dismissals).NotificationId);
        Assert.Contains("Dismissed", run.Output);
        Assert.Contains("34", run.Output);
        Assert.Empty(run.ErrorOutput.Trim());
    }

    [Fact]
    public void Dismiss_ReportsAMissingNotificationIdAsAUsageError()
    {
        AddProfile();

        var run = Run(["notification", "dismiss"]);

        Assert.Equal((int)ExitCode.UsageError, run.ExitCode);
        Assert.Empty(_inbox.Dismissals);
    }

    [Fact]
    public void Dismiss_ActsAsTheProfileNamedByTheOverride()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        var run = Run(["notification", "dismiss", "34", "--profile", "work"]);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("work", Assert.Single(_inbox.Dismissals).Profile);
    }

    /// <summary>
    ///     A script has nobody to answer a prompt, and stopping to ask one would make this command unusable in the
    ///     automation the CLI exists for. Typing the command is that invocation's consent.
    /// </summary>
    [Fact]
    public void Clear_EmptiesTheInboxWithoutAskingWhereThereIsNoTerminal()
    {
        AddProfile();

        var run = Run(["notification", "clear"], atATerminal: false);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("personal", Assert.Single(_inbox.Clearances));
        Assert.Contains("Cleared", run.Output);
    }

    /// <summary>
    ///     Clearing takes away the whole list at once and nothing brings it back, so a person at a terminal is asked
    ///     first — the same bargain <c>post delete</c> strikes, for the same reason.
    /// </summary>
    [Fact]
    public void Clear_AsksBeforeEmptyingTheInboxWhenThereIsSomebodyToAsk()
    {
        AddProfile();

        var run = Run(["notification", "clear"], atATerminal: true, typed: "y");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Single(_inbox.Clearances);
    }

    [Fact]
    public void Clear_LeavesTheInboxAloneWhenTheAnswerIsNo()
    {
        AddProfile();

        var run = Run(["notification", "clear"], atATerminal: true, typed: "n");

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Empty(_inbox.Clearances);
        Assert.Contains("Left", run.Output);
    }

    [Fact]
    public void Clear_DoesNotAskWhenTheCommandLineAlreadySaidYes()
    {
        AddProfile();

        var run = Run(["notification", "clear", "--yes"], atATerminal: true);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Single(_inbox.Clearances);
    }

    [Fact]
    public void Clear_ActsAsTheProfileNamedByTheOverride()
    {
        AddProfile();
        AddProfile("work", "hachyderm.io");

        var run = Run(["notification", "clear", "--profile", "work"], atATerminal: false);

        Assert.Equal((int)ExitCode.Success, run.ExitCode);
        Assert.Equal("work", Assert.Single(_inbox.Clearances));
    }

    private void AddProfile(string name = "personal", string instance = "mastodon.social") =>
        Run(["profile", "add", name, "--instance", instance, "--token", $"token-{name}"]);

    private CommandRun Run(string[] args, bool atATerminal = false, string? typed = null, int consoleWidth = 200)
    {
        var console = new TestConsole().Width(consoleWidth);
        var errorConsole = new TestConsole().Width(consoleWidth);

        if (atATerminal)
        {
            console.Interactive();
        }

        if (typed is not null)
        {
            console.Input.PushTextWithEnter(typed);
        }

        var app = WoolyCommandApp.Create(console, errorConsole, services =>
        {
            services.AddSingleton(new WoolyPaths(_directory.Path));
            services.AddSingleton<ICredentialStore>(new PlaintextFileCredentialStore(new WoolyPaths(_directory.Path)));
            services.AddSingleton<IAccessTokenVerifier>(FakeAccessTokenVerifier.Accepting());
            services.AddSingleton<INotificationInbox>(_inbox);
        });

        var exitCode = app.Run(args, TestContext.Current.CancellationToken);

        return new CommandRun(exitCode, console.Output, errorConsole.Output);
    }

    private sealed record CommandRun(int ExitCode, string Output, string ErrorOutput);
}
