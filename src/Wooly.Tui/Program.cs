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
using Wooly.Core.Timelines;
using Wooly.Tui;
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
        provider.GetRequiredService<IRateLimitReport>());

    var clock = provider.GetRequiredService<TimeProvider>();

    var shell = new Shell(
        profile,
        ports,
        new TerminalHost(application),
        clock,
        ShellTiming.Default,
        preferences.Hashtag);

    using var window = new ShellWindow(shell, Themes.ForCurrentTerminal(), clock, application.RequestStop);

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
