using Terminal.Gui.Drawing;
using Wooly.Core.Configuration;
using Wooly.Core.Errors;
using Wooly.Tests.Fakes;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tests.Tui;

/// <summary>
///     Which colours a role resolves to, and where they came from (#46). A theme is the one thing in the TUI that
///     holds a colour, so this is where a colour may be written down in a test — every other test asks about roles.
/// </summary>
public class ThemeTests
{
    private const string ConfigFile = "/somewhere/config.toml";

    /// <summary>Both built-ins answer every role, so no screen can meet one the theme has no colour for.</summary>
    [Fact]
    public void BothBuiltInThemesAnswerEveryRole()
    {
        foreach (var role in Enum.GetValues<Role>())
        {
            Themes.Dark.For(role);
            Themes.Light.For(role);
        }
    }

    /// <summary>
    ///     The two are told apart by what they are drawn on, which is what a reader chose one of them for: the light
    ///     one's page is brighter than the dark one's in every channel, and its text is darker than its own page.
    /// </summary>
    [Fact]
    public void TheLightThemeIsDrawnOnLightAndTheDarkOneOnDark()
    {
        var light = Themes.Light.For(Role.Body);
        var dark = Themes.Dark.For(Role.Body);

        Assert.True(light.Background.R > dark.Background.R);
        Assert.True(light.Background.G > dark.Background.G);
        Assert.True(light.Background.B > dark.Background.B);

        Assert.True(light.Foreground.R < light.Background.R);
        Assert.True(dark.Foreground.R > dark.Background.R);
    }

    [Fact]
    public void TheThemeIsTheOneTheConfigNamesByName()
    {
        Assert.Equal(Themes.Light.For(Role.Body), Chosen(new WoolyConfig { Theme = "light" }).For(Role.Body));
        Assert.Equal(Themes.Dark.For(Role.Body), Chosen(new WoolyConfig { Theme = "dark" }).For(Role.Body));
    }

    /// <summary>A file that says nothing about themes is every file written before this one existed.</summary>
    [Fact]
    public void WithNoThemeChosenItIsTheDarkOne() =>
        Assert.Equal(Themes.Dark.For(Role.BylineHandle), Chosen(WoolyConfig.Empty).For(Role.BylineHandle));

    /// <summary>
    ///     A theme is an override rather than a complete set, so that adding a role later does not break every config
    ///     file somebody wrote before it existed.
    /// </summary>
    [Fact]
    public void AThemeOverridesOnlyTheRolesItNames()
    {
        var theme = Chosen(Written("midnight", new ThemeConfig
        {
            Roles = new Dictionary<string, ThemeRole> { ["byline-handle"] = new("#ff0000") },
        }));

        Assert.Equal(new Color(255, 0, 0), theme.For(Role.BylineHandle).Foreground);
        Assert.Equal(Themes.Dark.For(Role.Muted), theme.For(Role.Muted));
    }

    /// <summary>Hex is written once and quantised by the driver; a name is one of the terminal's own sixteen.</summary>
    [Fact]
    public void AColourIsAHexTripleOrOneOfTheSixteenNames()
    {
        var written = Chosen(Written("midnight", new ThemeConfig
        {
            Roles = new Dictionary<string, ThemeRole> { ["link"] = new("#8fa8ff"), ["error"] = new("bright-red") },
        }));

        Assert.Equal(new Color(0x8f, 0xa8, 0xff), written.For(Role.Link).Foreground);
        Assert.Equal(new Color(ColorName16.BrightRed), written.For(Role.Error).Foreground);
    }

    /// <summary>The sixteen are the ANSI sixteen, whose two greys are the two that catch people out.</summary>
    [Theory]
    [InlineData("white", ColorName16.Gray)]
    [InlineData("bright-white", ColorName16.White)]
    [InlineData("bright-black", ColorName16.DarkGray)]
    [InlineData("red", ColorName16.Red)]
    public void TheSixteenNamesAreTheAnsiSixteen(string written, ColorName16 expected) =>
        Assert.Equal(new Color(expected), ColourName.Parse(written));

    /// <summary>
    ///     A triple and nothing else. A gap in the middle of one is a typo, and the worst thing to do with a typo
    ///     that reads as a hex number is to draw whatever it happens to say.
    /// </summary>
    [Theory]
    [InlineData("# ff000")]
    [InlineData("#fff")]
    [InlineData("#8fa8f")]
    [InlineData("8fa8ff")]
    [InlineData("brightblue")]
    public void AColourIsRefusedWhereItIsNotWrittenAsOne(string written) => Assert.Null(ColourName.Parse(written));

    /// <summary>
    ///     The background is a property of the theme rather than a role, so naming one moves everything that was on
    ///     the page — which is the whole of what naming one is for.
    /// </summary>
    [Fact]
    public void EveryRoleIsDrawnOnTheThemesOwnBackground()
    {
        var theme = Chosen(Written("midnight", new ThemeConfig { Background = "#000000" }));

        Assert.Equal(new Color(0, 0, 0), theme.For(Role.Body).Background);
        Assert.Equal(new Color(0, 0, 0), theme.For(Role.Muted).Background);
    }

    /// <summary>
    ///     Except the row somebody is standing on, which is told apart by the row rather than by the text on it — so
    ///     a theme that changes its foreground alone keeps the band it is drawn in.
    /// </summary>
    [Fact]
    public void ARoleWithABackgroundOfItsOwnKeepsItWhereAThemeDoesNotSayOtherwise()
    {
        var theme = Chosen(Written("midnight", new ThemeConfig
        {
            Background = "#000000",
            Roles = new Dictionary<string, ThemeRole> { ["selection"] = new("#ffffff") },
        }));

        Assert.Equal(Themes.Dark.For(Role.Selection).Background, theme.For(Role.Selection).Background);
        Assert.Equal(new Color(255, 255, 255), theme.For(Role.Selection).Foreground);
    }

    [Fact]
    public void ARoleMaySetABackgroundOfItsOwn()
    {
        var theme = Chosen(Written("midnight", new ThemeConfig
        {
            Roles = new Dictionary<string, ThemeRole> { ["selection"] = new(null, "#2a2942") },
        }));

        Assert.Equal(new Color(0x2a, 0x29, 0x42), theme.For(Role.Selection).Background);
        Assert.Equal(Themes.Dark.For(Role.Selection).Foreground, theme.For(Role.Selection).Foreground);
    }

    /// <summary>
    ///     Which built-in a theme is read against is its own brightness, because a theme that names a light page and
    ///     nothing else would otherwise be light text on light paper.
    /// </summary>
    [Fact]
    public void AThemeIsReadAgainstTheBuiltInOfItsOwnBrightness()
    {
        var theme = Chosen(Written("paper", new ThemeConfig { Background = "#fefefe" }));

        Assert.Equal(Themes.Light.For(Role.Body).Foreground, theme.For(Role.Body).Foreground);
    }

    /// <summary>A theme named after a built-in, and naming no page of its own, is that built-in with changes on it.</summary>
    [Fact]
    public void AThemeNamedAfterABuiltInIsReadAgainstIt()
    {
        var theme = Chosen(Written("light", new ThemeConfig
        {
            Roles = new Dictionary<string, ThemeRole> { ["error"] = new("#ff0000") },
        }));

        Assert.Equal(Themes.Light.For(Role.Body), theme.For(Role.Body));
        Assert.Equal(new Color(255, 0, 0), theme.For(Role.Error).Foreground);
    }

    /// <summary>
    ///     And the page beats the name where they disagree, because what a theme called <c>dark</c> with a white page
    ///     must not get is the built-in dark's near-white text on it.
    /// </summary>
    [Fact]
    public void APageBeatsTheNameItWasWrittenUnder()
    {
        var theme = Chosen(Written("dark", new ThemeConfig { Background = "#faf9fb" }));

        Assert.Equal(Themes.Light.For(Role.Body).Foreground, theme.For(Role.Body).Foreground);
    }

    /// <summary>
    ///     A role nobody has heard of is a typo, and a typo that paints nothing and says nothing is a reader looking
    ///     for a colour that was never going to arrive.
    /// </summary>
    [Fact]
    public void AThemeNamingARoleThatDoesNotExistSaysWhichOne()
    {
        var config = Written("midnight", new ThemeConfig
        {
            Roles = new Dictionary<string, ThemeRole> { ["byline-handel"] = new("#ff0000") },
        });

        var refused = Assert.Throws<ConfigurationException>(() => Chosen(config));

        Assert.Contains("byline-handel", refused.Message);
        Assert.Contains("midnight", refused.Message);
    }

    /// <summary>Every theme in the file is read, not only the one in use: a typo is a typo the day it is written.</summary>
    [Fact]
    public void AThemeNobodyChoseIsStillReadForTheRolesItNames()
    {
        var config = Written("midnight", new ThemeConfig
        {
            Roles = new Dictionary<string, ThemeRole> { ["byline-handel"] = new("#ff0000") },
        }) with { Theme = "dark" };

        Assert.Contains("byline-handel", Assert.Throws<ConfigurationException>(() => Chosen(config)).Message);
    }

    [Fact]
    public void AColourThisClientCannotReadSaysSoAndSaysWhatOneLooksLike()
    {
        var config = Written("midnight", new ThemeConfig
        {
            Roles = new Dictionary<string, ThemeRole> { ["body"] = new("rather blue") },
        });

        var refused = Assert.Throws<ConfigurationException>(() => Chosen(config));

        Assert.Contains("rather blue", refused.Message);
        Assert.Contains("#", refused.Message);
    }

    [Fact]
    public void AThemeBackgroundThisClientCannotReadSaysSo()
    {
        var config = Written("midnight", new ThemeConfig { Background = "off-white" });

        var refused = Assert.Throws<ConfigurationException>(() => Chosen(config));

        Assert.Contains("off-white", refused.Message);
        Assert.Contains("midnight", refused.Message);
    }

    /// <summary>A theme chosen but never written is the likeliest mistake of all, and the easiest to say.</summary>
    [Fact]
    public void AThemeNobodyWroteSaysWhatItWasCalled()
    {
        var refused = Assert.Throws<ConfigurationException>(() => Chosen(new WoolyConfig { Theme = "midnite" }));

        Assert.Contains("midnite", refused.Message);
        Assert.Contains("dark", refused.Message);
        Assert.Contains("light", refused.Message);
    }

    /// <summary>
    ///     A terminal that has said it wants no colour gets none, whatever the config file asked for — every state
    ///     the shell shows carries a glyph before it carries a colour (ADR-0014).
    /// </summary>
    [Theory]
    [InlineData("NO_COLOR", "1")]
    [InlineData("TERM", "dumb")]
    public void NoColourBeatsTheThemeChosen(string variable, string value)
    {
        using var set = new TemporaryEnvironmentVariable(variable) { Value = value };

        var theme = Themes.ForCurrentTerminal(new WoolyConfig { Theme = "light" }, ConfigFile);

        Assert.All(Enum.GetValues<Role>(), role => Assert.Equal(Attribute.Default, theme.For(role)));
    }

    /// <summary>
    ///     And the file is still read, so that a role misspelled in it is reported to everybody rather than only to
    ///     the readers whose terminals happen to have colour.
    /// </summary>
    [Fact]
    public void AMisspeltRoleIsStillReportedWhereThereIsNoColourToDrawIt()
    {
        using var set = new TemporaryEnvironmentVariable("NO_COLOR") { Value = "1" };

        var config = Written("midnight", new ThemeConfig
        {
            Roles = new Dictionary<string, ThemeRole> { ["byline-handel"] = new("#ff0000") },
        });

        Assert.Contains(
            "byline-handel",
            Assert.Throws<ConfigurationException>(() => Themes.ForCurrentTerminal(config, ConfigFile)).Message);
    }

    /// <summary>A terminal with colour draws the theme, which is the other half of the same answer.</summary>
    [Fact]
    public void ATerminalWithColourDrawsTheThemeThatWasChosen()
    {
        using var noColour = new TemporaryEnvironmentVariable("NO_COLOR") { Value = null };
        using var term = new TemporaryEnvironmentVariable("TERM") { Value = "xterm-256color" };

        var theme = Themes.ForCurrentTerminal(new WoolyConfig { Theme = "light" }, ConfigFile);

        Assert.Equal(Themes.Light.For(Role.Body), theme.For(Role.Body));
    }

    private static ITheme Chosen(WoolyConfig config) => Themes.Chosen(config, ConfigFile);

    private static WoolyConfig Written(string name, ThemeConfig theme) => new()
    {
        Theme = name,
        Themes = new Dictionary<string, ThemeConfig> { [name] = theme },
    };
}
