using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.App;
using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Conversations;
using Wooly.Core.Errors;
using Wooly.Core.Http;
using Wooly.Core.Notifications;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Core.Relationships;
using Wooly.Core.Search;
using Wooly.Core.Timelines;
using Wooly.Tui;
using Wooly.Tui.Media;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

var services = new ServiceCollection();
services.AddWoolyCore();

await using var provider = services.BuildServiceProvider();

try
{
    // Resolved the way a command's scope resolves one, including --profile: naming a profile here acts as that
    // profile for this run without changing which one is current (story 9).
    var profile = provider.GetRequiredService<IProfileRegistry>().Resolve(StartupProfile.NamedIn(args));
    var preferences = provider.GetRequiredService<IConfigStore>().Load().Preferences;

    // Terminal.Gui v2's instance-based application: no static/global shell state, so the TUI owns its own lifetime
    // (ADR-0002).
    using var application = Application.Create();
    application.Init();

    var ports = new ShellPorts(
        provider.GetRequiredService<ITimelineReader>(),
        provider.GetRequiredService<IPostAuthor>(),
        provider.GetRequiredService<IPostEngagement>(),
        provider.GetRequiredService<IAccountRelationships>(),
        provider.GetRequiredService<INotificationInbox>(),
        provider.GetRequiredService<IDirectMessages>(),
        provider.GetRequiredService<IInstanceSearch>(),
        provider.GetRequiredService<IRateLimitReport>());

    var clock = provider.GetRequiredService<TimeProvider>();

    var shell = new Shell(
        profile,
        ports,
        new TerminalHost(application),
        clock,
        ShellTiming.Default,
        preferences.Hashtag);

    // Story 49 asks for sixel first and Kitty where sixel is not there, which is the other way round from the order
    // Terminal.Gui tries them in. Settled once, here, before anything has been drawn (ADR-0016).
    RasterProtocol.PreferSixel(application.Driver);

    // A preview comes off a file server rather than off the API, so it goes out on its own client: it needs no token,
    // it counts against no rate limit, and a picture that will not load must not spend the retry budget a timeline's
    // fetch is relying on.
    using var files = new HttpClient { Timeout = Pictures.Patience };

    // A picture lands on whatever thread finished fetching it, and drawing is the application's. Redrawn rather than
    // told to the shell, because nothing about the shell has changed — the same rows are wanted, with the box that
    // was waiting now filled in.
    using var pictures = Pictures.Over(files, () => application.Invoke(() => application.LayoutAndDraw(true)));

    using var window = new ShellWindow(
        shell,
        Themes.ForCurrentTerminal(),
        clock,
        application.RequestStop,
        pictures);

    // Started rather than awaited: the first timeline arrives while the shell is already on screen, which is what the
    // breadcrumb's fetching mark is for.
    window.Initialized += (_, _) => _ = shell.Open();

    application.Run(window);

    return (int)TuiExit.Success;
}
catch (WoolyException failure)
{
    // Before a screen exists there is nowhere to say this but here: no profile set up, a config file that names one
    // that is not there, or a token that has been revoked. A rate limit inside the shell is waited out instead.
    await Console.Error.WriteLineAsync(failure.Message);

    return (int)TuiExit.Failed;
}
