using Terminal.Gui.App;
using Wooly.Core.Configuration;
using Wooly.Tests.Fakes;
using Wooly.Tui.Theme;
using Wooly.Tui.Views;

namespace Wooly.Tests.Tui;

/// <summary>
///     What reaches the terminal when a theme's page is its own (#267): asked of a real frame, drawn headless, because
///     the promise is about the bytes written rather than the colour a role resolves to.
/// </summary>
public class TerminalsOwnPageTests
{
    /// <summary>The escape that says "the terminal's own background", rather than any colour of this client's.</summary>
    private const string OwnBackground = "\u001b[49m";

    [Fact]
    public async Task ABuiltInThemeWritesTheTerminalsOwnBackgroundForThePage()
    {
        var frame = await Frame(Themes.Dark);

        Assert.Contains(OwnBackground, frame, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AThemeNamingAHexPageStillDrawsOnIt()
    {
        var theme = Themes.Chosen(
            new WoolyConfig
            {
                Theme = "midnight",
                Themes = new Dictionary<string, ThemeConfig> { ["midnight"] = new() { Background = "#12111a" } },
            },
            "/somewhere/config.toml");

        var frame = await Frame(theme);

        Assert.Contains("48;2;18;17;26", frame, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnBackground, frame, StringComparison.Ordinal);
    }

    /// <summary>The shell opened on its first timeline and drawn once, as the bytes a terminal would be sent.</summary>
    private static async Task<string> Frame(ITheme theme)
    {
        var built = new AShell();
        var shell = await built.Opened();

        using var application = Application.Create();
        application.Init("ansi");
        application.Driver!.SetScreenSize(80, 24);

        using var window = new ShellWindow(shell, theme, built.Clock, () => { }, FakePictures.DrawingNothing());
        application.Begin(window);
        application.LayoutAndDraw(true);

        return application.Driver.ToAnsi();
    }
}
