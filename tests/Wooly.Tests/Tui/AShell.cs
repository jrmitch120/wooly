using Wooly.Core;
using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tests.Tui;

/// <summary>
///     A shell built over fakes, so a test says only the part it is about. The seam is the same one
///     <c>tests/Wooly.Tests/Cli</c> uses — the <c>Wooly.Core</c> ports — because the TUI is a second front end over
///     them and not a second way of reaching an instance (ADR-0005).
/// </summary>
internal sealed class AShell
{
    /// <summary>The moment every test's clock starts at.</summary>
    public static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    public AShell()
    {
        Host = new FakeShellHost();
        Clock = new MovableTimeProvider(Now);
        Timelines = FakeTimelineReader.Holding(APost.With());
        Author = FakePostAuthor.Answering();
        Engagement = FakePostEngagement.Answering();
        Accounts = FakeAccountRelationships.Holding();
        Notifications = FakeNotificationInbox.Holding();
        Messages = FakeDirectMessages.Holding();
        Search = FakeInstanceSearch.Finding();
        Suggestions = FakeFollowSuggestions.Offering();
        RateLimit = FakeRateLimitReport.Silent();
    }

    public FakeShellHost Host { get; }

    public MovableTimeProvider Clock { get; }

    public FakeTimelineReader Timelines { get; set; }

    public FakePostAuthor Author { get; set; }

    public FakePostEngagement Engagement { get; set; }

    public FakeAccountRelationships Accounts { get; set; }

    public FakeNotificationInbox Notifications { get; set; }

    public FakeDirectMessages Messages { get; set; }

    public FakeInstanceSearch Search { get; set; }

    public FakeFollowSuggestions Suggestions { get; set; }

    public FakeRateLimitReport RateLimit { get; set; }

    /// <summary>
    ///     Where an address goes, which is the one thing the shell does that leaves the terminal (#85). Not one of the
    ///     ports, for the reason <c>ShellPorts</c> gives: a browser is not on an instance.
    /// </summary>
    public FakeWebBrowser Browser { get; set; } = new();

    /// <summary>
    ///     The profiles set up on this machine, which is the profile every test acts as and nobody else — and it the
    ///     current one, as a launch without <c>--profile</c> would have it. Not one of the ports either, for the reason
    ///     <c>ProfilePorts</c> gives: this is the local config, not an instance.
    /// </summary>
    public FakeProfileRegistry Profiles { get; set; } = FakeProfileRegistry.Holding(
        "personal",
        FakeProfileRegistry.Profile("personal", "mastodon.social", "jeff@mastodon.social"));

    /// <summary>Where this machine's files are, which only the plaintext-token warning ever names.</summary>
    public WoolyPaths Paths { get; set; } = new("/home/jeff/.config/wooly");

    /// <summary>The profile every test acts as, which owns the posts <see cref="APost" /> builds.</summary>
    public ActiveProfile Profile { get; set; } = new()
    {
        Name = "personal",
        Instance = "mastodon.social",
        Account = "jeff@mastodon.social",
        AccessToken = "token-personal",
    };

    /// <summary>The hashtag the rail keeps a place for, or none.</summary>
    public string? Hashtag { get; set; }

    /// <summary>
    ///     How long the settle window and the cache are. Real lengths, because the fake host is what decides when a
    ///     wait happens and the clock is what decides how old a cache entry is — neither of them passes on its own.
    /// </summary>
    public ShellTiming Timing { get; set; } = ShellTiming.Default;

    /// <summary>The shell itself, over whatever the fakes have been set to.</summary>
    public Shell Build() => new(
        Profile,
        new ShellPorts(Timelines, Author, Engagement, Accounts, Notifications, Messages, Search, Suggestions, RateLimit),
        new ProfilePorts(Profiles, Paths),
        Host,
        Browser,
        Clock,
        Timing,
        Hashtag);

    /// <summary>
    ///     What <paramref name="screen" /> draws at 61 columns, past the one column the gutter takes — which every
    ///     list on this shell stamps and no screen draws itself, so a test asserting on a row would otherwise be
    ///     asserting on the pick marker too.
    /// </summary>
    /// <remarks>
    ///     Here rather than at each test class, so that "a row" means the same thing in every one of them: two copies
    ///     that disagreed about the gutter would be two ideas of what is on screen.
    /// </remarks>
    public static IReadOnlyList<string> Drawn(Screen screen) =>
    [
        .. screen.Lines(new Drawing(61, Now)).Select(line => line.Text.Length > 0 ? line.Text[1..] : line.Text),
    ];

    /// <summary>
    ///     How many requests every port that reaches an instance has been asked, all told — where a test proves that
    ///     something fetched nothing.
    /// </summary>
    public int Requests =>
        Timelines.Reads.Count
        + Author.Published.Count + Author.Edits.Count + Author.Deletions.Count
        + Engagement.Marks.Count + Engagement.Reads.Count + Engagement.ThreadsRead.Count + Engagement.Votes.Count
        + Accounts.Ties.Count + Accounts.Lists.Count + Accounts.Answers.Count + Accounts.Reads.Count
        + Accounts.Familiars.Count + Accounts.Standings.Count
        + Notifications.Reads.Count + Notifications.Dismissals.Count + Notifications.Clearances.Count
        + Messages.Listings.Count + Messages.Shown.Count + Messages.MarkedRead.Count
        + Search.Searches.Count
        + Suggestions.Reads.Count + Suggestions.Dismissals.Count;

    /// <summary>A shell that has already opened onto its first destination.</summary>
    public async Task<Shell> Opened()
    {
        var shell = Build();

        await shell.Open();

        Host.Drain();

        return shell;
    }
}
