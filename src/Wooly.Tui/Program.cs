using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui.App;
using Wooly.Core;
using Wooly.Core.Configuration;
using Wooly.Core.Conversations;
using Wooly.Core.Discovery;
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
    // profile for this run without changing which one is current (story 9). Nobody to act as opens on adding a
    // profile rather than failing here; what is still thrown is said below, before a screen exists (#247).
    var opening = Opening.Of(provider.GetRequiredService<IProfileRegistry>(), StartupProfile.NamedIn(args));
    var config = provider.GetRequiredService<IConfigStore>().Load();

    // Read before a screen exists, because a theme naming a role or a colour this client cannot make sense of is a
    // config error, and the place to say one is here — where there is still a console to say it on.
    var theme = Themes.ForCurrentTerminal(config, provider.GetRequiredService<WoolyPaths>().ConfigFile);

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
        provider.GetRequiredService<IFollowSuggestions>(),
        provider.GetRequiredService<IRateLimitReport>(),
        provider.GetRequiredService<IInstanceLimits>(),
        provider.GetRequiredService<IAccountDefaults>());

    var clock = provider.GetRequiredService<TimeProvider>();

    var shell = new Shell(
        opening,
        ports,

        // This machine's profiles, which are the local config rather than anything on an instance — so, like the
        // browser below, not one of the ports above (ADR-0020).
        new ProfilePorts(
            provider.GetRequiredService<IProfileRegistry>(),
            provider.GetRequiredService<WoolyPaths>(),
            provider.GetRequiredService<IBrowserAuthorizer>(),
            provider.GetRequiredService<IAccessTokenVerifier>()),
        new TerminalHost(application),

        // The same browser the sign-in sends somebody to (ADR-0004), and deliberately not one of the ports above:
        // those are what the shell reaches an instance through, and a browser is not on one (#85).
        provider.GetRequiredService<IWebBrowser>(),
        clock,
        ShellTiming.Default,
        config.Preferences);

    // A preview comes off a file server rather than off the API, so it goes out on its own client: it needs no token,
    // it counts against no rate limit, and a picture that will not load must not spend the retry budget a timeline's
    // fetch is relying on.
    using var files = new HttpClient { Timeout = Pictures.Patience };

    // A picture lands on whatever thread finished fetching it, and drawing is the application's. Redrawn rather than
    // told to the shell, because nothing about the shell has changed — the same rows are wanted, with the box that
    // was waiting now filled in.
    //
    // One redraw for a burst, not one each: pictures arrive together, a forced redraw re-encodes every picture on
    // screen, and doing that once per arrival is what a reader sees as flicker.
    var redrawing = 0;

    void Redraw()
    {
        if (Interlocked.Exchange(ref redrawing, 1) == 1)
        {
            return;
        }

        application.Invoke(() =>
        {
            Interlocked.Exchange(ref redrawing, 0);
            application.LayoutAndDraw(true);
        });
    }

    // Ghostty and kitty say who they are in the environment, which is known before the first frame — and they are the
    // terminals known to draw Kitty's placeholders, which WezTerm takes and then prints as boxes (#292, ADR-0022).
    var placeholdersByName = KnownTerminal.DrawsPlaceholders(Environment.GetEnvironmentVariable);

    // The cell as the kernel measures it, which is the real one: Terminal.Gui's guess stretched every photograph.
    // Measured again when the screen changes size, a change of font size included.
    var windowSize = new WindowSize(
        () => (application.Driver?.Cols ?? 0, application.Driver?.Rows ?? 0),
        WindowSize.Measure);

    // On a Kitty terminal a picture is sent once and drawn as text (ADR-0022). Written on the UI thread, which is the
    // only one a frame asks from; the PNG it sends is encoded off it, and a redraw brings the picture in once it is.
    // Disposed before the application is, which is what takes every picture this run sent off the terminal.
    using var placeholders = new Placeholders(
        new KittyImages(sequence => application.Driver?.GetOutput().Write(sequence)),
        placeholdersByName,
        Redraw,
        work => Task.Run(work));

    // Decoded to the content region's width, the widest a picture's box can be, at the cell of the Raster the window
    // worked out for the frame (ADR-0025, #357). The window is built after the cache it draws from, so the cache asks
    // it through this; a frame only asks once the window is drawing, and nought before then decodes to the decoder's
    // own bounds.
    ShellWindow? shellWindow = null;

    using var pictures = Pictures.Over(
        files,
        () => shellWindow?.Raster.Cell,
        Redraw,
        () => shellWindow?.ContentColumns ?? 0);

    // Each frame with pictures in it written whole, where the terminal can hold one back until it is (#342).
    var frames = new SynchronizedFrames(sequence => application.Driver?.GetOutput().Write(sequence));

    frames.Over(application);

    using var window = shellWindow = new ShellWindow(
        shell,
        theme,
        clock,
        application.RequestStop,
        pictures,
        config.Preferences.HideDrawnCaption,
        placeholders,
        new Blurs(),
        frames: frames,

        // How this terminal paints pixels, worked out here and nowhere else, once a frame: the terminal answers its
        // sixel and Kitty questions some frames after the shell is on screen, and a change of font size changes the
        // cell.
        raster: () => Raster.Of(application.Driver, placeholdersByName, () => windowSize.Cell));

    // A paste arrives as one string rather than as keys, before it is handed to whatever has focus. The shell takes
    // it where one of its own fields is typing, and leaves it to whatever has focus everywhere else — the compose
    // editor or its warning field above all, which take their own pastes (#320).
    application.Paste += (_, pasted) => pasted.Handled = shell.Paste(pasted.Text);

    // Started rather than awaited: the first timeline arrives while the shell is already on screen, which is what the
    // breadcrumb's spinner is for.
    window.Initialized += (_, _) => _ = shell.Open();

    application.Run(window);

    return (int)TuiExit.Success;
}
catch (WoolyException failure)
{
    // Before a screen exists there is nowhere to say this but here: a config file that cannot be read or names a
    // default that is not there, or a --profile naming nothing — each a mistake made where it is said. Nobody to act
    // as, or a token missing or refused, opens the shell on adding a profile instead (#247). A rate limit inside the
    // shell is waited out.
    await Console.Error.WriteLineAsync(failure.Message);

    return (int)TuiExit.Failed;
}
