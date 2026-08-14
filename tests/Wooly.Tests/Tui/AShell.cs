using Wooly.Core.Profiles;
using Wooly.Tests.Fakes;
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

    public FakeRateLimitReport RateLimit { get; set; }

    /// <summary>
    ///     Where an address goes, which is the one thing the shell does that leaves the terminal (#85). Not one of the
    ///     ports, for the reason <c>ShellPorts</c> gives: a browser is not on an instance.
    /// </summary>
    public FakeWebBrowser Browser { get; set; } = new();

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
        new ShellPorts(Timelines, Author, Engagement, Accounts, Notifications, Messages, Search, RateLimit),
        Host,
        Browser,
        Clock,
        Timing,
        Hashtag);

    /// <summary>A shell that has already opened onto its first destination.</summary>
    public async Task<Shell> Opened()
    {
        var shell = Build();

        await shell.Open();

        Host.Drain();

        return shell;
    }
}
