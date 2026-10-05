using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Credentials;
using Wooly.Core.Http;
using Wooly.Core.Notifications;
using Wooly.Core.Paging;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Timelines;
using Wooly.Tests.Fakes;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>⏎</c> on the profiles screen: the session acts as the picked profile from here on, starting again on Home as
///     if it had been launched with it — and nothing fetched as the old profile reaches the new one (ADR-0020, #243).
/// </summary>
public class ShellSwitchTests
{
    /// <summary>Acting as personal, which is the default, with work set up beside it on another instance.</summary>
    private static AShell PersonalAndWork() => new()
    {
        Profiles = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", "jeff@hachyderm.io")),
    };

    /// <summary>The profiles screen, with <paramref name="name" /> picked.</summary>
    private static void OnProfiles(Shell shell, string name)
    {
        shell.Press(ShellKey.CtrlP);

        Assert.IsType<ProfilesScreen>(shell.Screen).Pick(name);
    }

    /// <summary>Switches to <paramref name="name" />, and lets everything the switch asked for land.</summary>
    private static void SwitchTo(AShell shell, Shell opened, string name)
    {
        OnProfiles(opened, name);

        Assert.True(opened.Press(ShellKey.Enter));

        shell.Host.Drain();
    }

    [Fact]
    public async Task Enter_StartsAgainOnHome_ReadingTheNewProfilesHomeTimeline_WithItsToken()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();

        opened.Press(ShellKey.Tab);
        shell.Host.Settle();
        shell.Timelines.Reads.Clear();
        shell.Timelines.Tokens.Clear();

        SwitchTo(shell, opened, "work");

        Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(1, opened.Depth);
        Assert.Equal(DestinationKind.Home, opened.Rail.Showing.Kind);
        Assert.Equal(0, opened.Rail.Cursor);

        var read = Assert.Single(shell.Timelines.Reads);
        Assert.Equal("work", read.Profile);
        Assert.Equal(Timeline.Home, read.Timeline);
        Assert.Equal("token-work", Assert.Single(shell.Timelines.Tokens));
    }

    /// <summary>Every request from here on is the new profile's — the rail's counts first among them.</summary>
    [Fact]
    public async Task EveryRequestAfterTheSwitch_GoesOutWithTheNewProfilesToken()
    {
        var shell = PersonalAndWork();
        shell.Engagement = FakePostEngagement.Answered(APost.With(id: "110"));
        var opened = await shell.Opened();

        List<string>[] tokens =
        [
            shell.Timelines.Tokens, shell.Author.Tokens, shell.Engagement.Tokens, shell.Accounts.Tokens,
            shell.Notifications.Tokens, shell.Messages.Tokens, shell.Search.Tokens, shell.Suggestions.Tokens,
            shell.Defaults.Tokens,
        ];
        Array.ForEach(tokens, list => list.Clear());

        SwitchTo(shell, opened, "work");

        // A drill, a mark, the rail and a search — every kind of question the shell puts.
        await opened.Enter();
        shell.Host.Drain();
        await opened.Mark(PostMark.Favorite);
        shell.Host.Drain();
        opened.Press(ShellKey.Escape);
        opened.Press(ShellKey.Tab);
        shell.Host.Settle();

        var after = shell.Tokens.ToList();

        Assert.NotEmpty(after);
        Assert.All(after, token => Assert.Equal("token-work", token));
    }

    /// <summary>The rail is the new profile's: its handle on the Profile destination, and its instance on the foot.</summary>
    [Fact]
    public async Task TheRail_NamesTheNewProfile()
    {
        var shell = PersonalAndWork();
        shell.Profiles = FakeProfileRegistry.Holding(
            "personal",
            FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"),
            FakeProfileRegistry.Profile("work", "hachyderm.io", "jeffrey@hachyderm.io"));
        var opened = await shell.Opened();

        SwitchTo(shell, opened, "work");

        Assert.Equal("hachyderm.io", opened.Instance);
        Assert.Contains(opened.Rail.Destinations, destination => destination.Label == "@jeffrey");
        Assert.DoesNotContain(opened.Rail.Destinations, destination => destination.Label == "@jeff");
    }

    /// <summary>
    ///     A count read as the old profile is not the new one's: every badge starts again from what the new profile's
    ///     own reads say.
    /// </summary>
    [Fact]
    public async Task UnreadCounts_AreTheNewProfilesOwn()
    {
        var shell = PersonalAndWork();
        shell.Notifications = FakeNotificationInbox.Holding(ANotification.Follow());
        shell.Notifications.ByProfile["work"] = () => Task.FromResult(Fetch<Notification>.Complete([]));
        var opened = await shell.Opened();

        Assert.Equal(1, Badge(opened, DestinationKind.Notifications));

        SwitchTo(shell, opened, "work");

        Assert.Equal(0, Badge(opened, DestinationKind.Notifications));
    }

    /// <summary>A count read as the old profile that lands after the switch is not drawn on the new one's rail.</summary>
    [Fact]
    public async Task ACountInFlightAtTheSwitch_NeverLands()
    {
        var shell = PersonalAndWork();
        var late = new TaskCompletionSource<Fetch<Notification>>();
        shell.Notifications.ByProfile["personal"] = () => late.Task;
        var opened = shell.Build();

        // The old profile's count is put, and not yet answered, when the switch happens.
        var opening = opened.Open();
        shell.Host.Drain();

        SwitchTo(shell, opened, "work");

        late.SetResult(Fetch<Notification>.Complete([ANotification.Follow("1"), ANotification.Follow("2")]));
        await opening;
        shell.Host.Drain();

        Assert.Equal(0, Badge(opened, DestinationKind.Notifications));
    }

    /// <summary>
    ///     The old profile's timeline, asked for before the switch and answered after it, is thrown away rather than
    ///     drawn under the new one.
    /// </summary>
    [Fact]
    public async Task AFetchInFlightAtTheSwitch_NeverDraws()
    {
        var theirs = new TaskCompletionSource<Fetch<Post>>();
        var shell = PersonalAndWork();
        shell.Timelines = FakeTimelineReader.Awaiting(timeline => timeline == Timeline.Local
            ? theirs.Task
            : Task.FromResult(Fetch<Post>.Complete([APost.With(id: "home-1")])));
        var opened = await shell.Opened();

        opened.Press(ShellKey.Tab);
        shell.Host.Settle();

        Assert.True(opened.Fetching);

        SwitchTo(shell, opened, "work");

        theirs.SetResult(Fetch<Post>.Complete([APost.With(id: "local-1")]));
        shell.Host.Drain();
        shell.Host.SettleAll();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.Equal(DestinationKind.Home, opened.Rail.Showing.Kind);
        Assert.Equal(["home-1"], feed.Posts.Select(post => post.Id));
        Assert.Equal("work", shell.Timelines.Reads[^1].Profile);
        Assert.False(opened.Fetching);
    }

    /// <summary>
    ///     An action the instance has already received is not presented as undone — nor is the answer to it drawn on
    ///     the new profile's screens, where the post it changed may not even be.
    /// </summary>
    [Fact]
    public async Task AMarkInFlightAtTheSwitch_ChangesNothingOnTheNewProfilesScreens()
    {
        var shell = PersonalAndWork();
        var marked = new TaskCompletionSource<Post>();
        shell.Timelines = FakeTimelineReader.Holding(APost.With(id: "110"));
        var opened = await shell.Opened();

        shell.Engagement.Marking = () => marked.Task;
        var marking = opened.Mark(PostMark.Favorite);

        SwitchTo(shell, opened, "work");

        marked.SetResult(APost.With(id: "110", marks: APost.Marked(favorited: true)));
        await marking;
        shell.Host.Drain();

        var feed = Assert.IsType<FeedScreen>(opened.Screen);
        Assert.False(feed.Posts.Single().Marks.Has(PostMark.Favorite));
        Assert.Null(opened.Notice);
    }

    /// <summary>A confirmation asked as the old profile cannot be agreed to as the new one.</summary>
    [Fact]
    public async Task APendingConfirmation_IsDismissed()
    {
        var shell = PersonalAndWork();
        shell.Timelines = FakeTimelineReader.Holding(APost.With(id: "110", isMine: true));
        var opened = await shell.Opened();

        opened.AskToDelete();
        Assert.NotNull(opened.Asking);

        SwitchTo(shell, opened, "work");

        Assert.Null(opened.Asking);
        Assert.Empty(shell.Author.Deletions);
    }

    /// <summary>The quota the rail's foot draws was the old profile's budget, so it is not drawn for the new one.</summary>
    [Fact]
    public async Task TheQuota_IsNotCarriedAcross()
    {
        var shell = PersonalAndWork();
        shell.RateLimit = FakeRateLimitReport.Of(250);
        var opened = await shell.Opened();

        Assert.NotNull(opened.Quota);

        SwitchTo(shell, opened, "work");

        Assert.Null(opened.Quota);

        // And the new profile's own, once an instance has said it.
        shell.RateLimit.Latest = new RateLimitQuota(299, 300, null);

        Assert.Equal(299, opened.Quota?.Remaining);
    }

    /// <summary>Only for this session: the default profile is left where it was.</summary>
    [Fact]
    public async Task Switching_LeavesTheConfigFileByteForByteAsItWas()
    {
        using var directory = new TemporaryDirectory();
        var paths = new WoolyPaths(directory.Path);
        var registry = new ProfileRegistry(new TomlConfigStore(paths), new PlaintextFileCredentialStore(paths), paths);
        registry.Add(
            "personal",
            new ProfileConfig { Instance = "mastodon.social", Account = "jeff@mastodon.social" },
            "token-personal");
        registry.Add(
            "work",
            new ProfileConfig { Instance = "hachyderm.io", Account = "jeff@hachyderm.io" },
            "token-work");

        var config = await File.ReadAllBytesAsync(paths.ConfigFile, TestContext.Current.CancellationToken);
        var credentials = await File.ReadAllBytesAsync(paths.CredentialFile, TestContext.Current.CancellationToken);

        var shell = new AShell();
        var opened = await shell.OpenedOver(registry, paths);

        SwitchTo(shell, opened, "work");

        Assert.Equal("work", shell.Timelines.Reads[^1].Profile);
        Assert.Equal(config, await File.ReadAllBytesAsync(paths.ConfigFile, TestContext.Current.CancellationToken));
        Assert.Equal(credentials, await File.ReadAllBytesAsync(paths.CredentialFile, TestContext.Current.CancellationToken));
        Assert.Equal("personal", registry.Resolve(null).Name);
    }

    /// <summary>
    ///     A profile that cannot be resolved — its token gone from the store — is not switched to: the session stays
    ///     exactly as it was, and says why.
    /// </summary>
    [Fact]
    public async Task AProfileThatCannotBeResolved_LeavesTheSessionAsItWas_WithANotice()
    {
        var shell = PersonalAndWork();
        shell.Profiles.Tokenless.Add("work");
        var opened = await shell.Opened();
        var requests = shell.Requests;

        OnProfiles(opened, "work");
        var screen = opened.Screen;

        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Same(screen, opened.Screen);
        Assert.Equal(2, opened.Depth);
        Assert.Equal(requests, shell.Requests);
        Assert.True(opened.NoticeIsError);
        Assert.Contains("no access token", opened.Notice);

        // Still acting as personal: the list says so, and the next request goes out with personal's token.
        Assert.Contains(AShell.Drawn(opened.Screen), row => row.StartsWith("personal") && row.Contains("acting as"));

        opened.Press(ShellKey.Escape);
        await opened.Refresh();
        shell.Host.Drain();

        Assert.Equal("token-personal", shell.Tokens.Last());
    }

    /// <summary>⏎ on the row already acted as does nothing — and so is not offered there.</summary>
    [Fact]
    public async Task Enter_OnTheProfileAlreadyActedAs_DoesNothing_AndIsNotOffered()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();
        var requests = shell.Requests;

        OnProfiles(opened, "personal");

        Assert.DoesNotContain(opened.Keys, key => key.Key == "⏎");

        var screen = opened.Screen;
        opened.Press(ShellKey.Enter);
        shell.Host.Drain();

        Assert.Same(screen, opened.Screen);
        Assert.Equal(requests, shell.Requests);
    }

    /// <summary>And on any other row it is offered, as what it does.</summary>
    [Fact]
    public async Task Enter_IsOffered_OnAnyOtherProfile()
    {
        var opened = await PersonalAndWork().Opened();

        OnProfiles(opened, "work");

        Assert.Contains(opened.Keys, key => key is { Key: "⏎", Does: "act as" });
    }

    /// <summary>The profiles screen, opened again after a switch, marks the new profile as the one acted as.</summary>
    [Fact]
    public async Task AfterASwitch_TheProfilesScreenMarksTheNewProfileActedAs()
    {
        var shell = PersonalAndWork();
        var opened = await shell.Opened();

        SwitchTo(shell, opened, "work");
        opened.Press(ShellKey.CtrlP);

        var drawn = AShell.Drawn(opened.Screen);
        Assert.Contains(drawn, row => row.StartsWith("work") && row.Contains("acting as"));
        Assert.Contains(drawn, row => row.StartsWith("personal") && !row.Contains("acting as") && row.Contains("default"));
    }

    private static int Badge(Shell shell, DestinationKind kind) =>
        shell.Rail.Destinations.Single(destination => destination.Kind == kind).Unread;
}
