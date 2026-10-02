using Terminal.Gui.Drawing;
using Wooly.Core.Configuration;
using Wooly.Tui.Theme;

namespace Wooly.Tests.Tui;

/// <summary>
///     What reaches the terminal when a theme's background is the terminal's own (#267): asked of a real frame, drawn headless, because
///     the promise is about the bytes written rather than the colour a role resolves to.
/// </summary>
public class TerminalsOwnBackgroundTests
{
    /// <summary>The escape that says "the terminal's own background", rather than any colour of this client's.</summary>
    private const string OwnBackground = "\u001b[49m";

    /// <summary>The parameters of a truecolor background of <c>#12111a</c>, which is how a hex page reaches the terminal.</summary>
    private const string HexBackground = "48;2;18;17;26";

    [Fact]
    public async Task ABuiltInThemeWritesTheTerminalsOwnBackground()
    {
        var frame = await Frame(Themes.Dark);

        Assert.Contains(OwnBackground, frame, StringComparison.Ordinal);
    }

    /// <summary>
    ///     And nothing else is written for the page: a cell is on the terminal's own background or on a band, which
    ///     is the one kind of role that brings a background of its own.
    /// </summary>
    [Fact]
    public async Task ABuiltInThemeDrawsNoBackgroundOfItsOwnBesideItsBands()
    {
        var bands = Enum.GetValues<Role>().Select(role => Themes.Dark.For(role).Background).ToHashSet();
        using var shown = await DrawnShell.Of(80, 24, Themes.Dark);
        var drawn = shown.Cells().Select(cell => cell.Background).ToList();

        Assert.Contains(Color.None, drawn);
        Assert.All(drawn, background => Assert.Contains(background, bands));
    }

    [Fact]
    public async Task AThemeNamingAHexBackgroundStillDrawsOnIt()
    {
        var theme = Themes.Chosen(
            new WoolyConfig
            {
                Theme = "midnight",
                Themes = new Dictionary<string, ThemeConfig> { ["midnight"] = new() { Background = "#12111a" } },
            },
            "/somewhere/config.toml");

        var frame = await Frame(theme);

        Assert.Contains(HexBackground, frame, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnBackground, frame, StringComparison.Ordinal);
    }

    /// <summary>The shell opened on its first timeline and drawn once, as the bytes a terminal would be sent.</summary>
    private static async Task<string> Frame(ITheme theme)
    {
        using var drawn = await DrawnShell.Of(80, 24, theme);

        return drawn.Ansi();
    }
}
