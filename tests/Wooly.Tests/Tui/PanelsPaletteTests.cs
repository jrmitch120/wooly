using Terminal.Gui.Drawing;
using Wooly.Tui.Theme;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tests.Tui;

/// <summary>
///     <c>dark</c> is the panels palette tuned in the prototype ADR-0021 chose — skin B2 on <c>prototype/tui-skins</c>,
///     the stacked rail (#273): Catppuccin Mocha accents on the terminal's own background, blue the one accent. Like <see cref="ThemeTests" />, a place a colour may be written
///     down in a test — here the prototype's own, role for role, so that the built-in is checked against the design
///     rather than against itself.
/// </summary>
public class PanelsPaletteTests
{
    private const string Text = "#cdd6f4", Subtext = "#a6adc8", Overlay = "#6c7086", Surface0 = "#313244";
    private const string Surface1 = "#45475a", Surface2 = "#585b70", Mauve = "#cba6f7", Blue = "#89b4fa";
    private const string Sapphire = "#74c7ec", Teal = "#94e2d5", Green = "#a6e3a1", Yellow = "#f9e2af";
    private const string Peach = "#fab387", Red = "#f38ba8";

    /// <summary>The pick mark's near-white.</summary>
    private const string PickMark = "#f2f0f7";

    /// <summary>The picked thing's band: a lift off black rather than a shade of grey.</summary>
    private const string Band = "#16171c";

    /// <summary>The rail's current entry, a step lighter than the post band.</summary>
    private const string RailBand = "#1f2128";

    /// <summary>The prototype's colour for every role, and the band of the roles that bring one of their own.</summary>
    private static readonly Dictionary<Role, (string Foreground, string? Background)> ThePrototypes = new()
    {
        [Role.Body] = (Text, null),
        [Role.Hashtag] = (Teal, null),
        [Role.Mention] = (Mauve, null),
        [Role.Link] = (Blue, null),
        [Role.Muted] = (Overlay, null),
        [Role.BylineName] = (Text, null),
        [Role.BylineHandle] = (Sapphire, null),
        [Role.Audience] = (Overlay, null),
        [Role.ContentWarning] = (Peach, null),
        [Role.Media] = (Teal, null),
        [Role.Poll] = (Mauve, null),
        [Role.ReferencePicked] = (PickMark, null),
        [Role.Boost] = (Green, null),
        [Role.BoostMine] = (Green, null),
        [Role.Favorite] = (Yellow, null),
        [Role.FavoriteMine] = (Yellow, null),
        [Role.Replies] = (Mauve, null),
        [Role.Selection] = (PickMark, null),
        [Role.Band] = (PickMark, Band),
        [Role.Rail] = (Subtext, null),
        [Role.RailCurrent] = (Blue, RailBand),
        [Role.RailCursor] = (Text, Surface0),
        [Role.RailUnread] = (Peach, null),
        [Role.Quota] = (Overlay, null),
        [Role.QuotaLow] = (Red, null),
        [Role.Gauge] = (Blue, null),
        [Role.GaugeEmpty] = (Surface1, null),
        [Role.Chrome] = (Surface2, null),
        [Role.Key] = (Sapphire, null),
        [Role.PanelBorder] = (Surface2, null),
        [Role.PanelBorderActive] = (Blue, null),
        [Role.PanelTitle] = (Blue, null),
        [Role.Loading] = (Overlay, null),
        [Role.Spinner] = (Peach, null),
        [Role.Destructive] = (Red, null),
        [Role.Error] = (Red, null),
    };

    public static TheoryData<Role> Roles() => [.. Enum.GetValues<Role>()];

    /// <summary>The table above is every role, so a role added later is a role this palette has to be asked about.</summary>
    [Fact]
    public void ThePrototypesTableIsEveryRole() => Assert.Equal(Enum.GetValues<Role>().Order(), ThePrototypes.Keys.Order());

    [Theory]
    [MemberData(nameof(Roles))]
    public void TheDarkThemeIsThePrototypesColourForEveryRole(Role role)
    {
        var (foreground, background) = ThePrototypes[role];
        var drawn = Themes.Dark.For(role);

        Assert.Equal(Colour(foreground), drawn.Foreground);
        Assert.Equal(background is null ? Color.None : Colour(background), drawn.Background);
    }

    /// <summary>
    ///     <c>light</c> is the same structure in light values: two roles share a colour in one exactly where they share
    ///     it in the other, and the same roles bring a band of their own.
    /// </summary>
    [Fact]
    public void TheLightThemeHasTheDarkOnesStructure()
    {
        var roles = Enum.GetValues<Role>();

        foreach (var one in roles)
        {
            Assert.Equal(
                Themes.Dark.For(one).Background == Color.None,
                Themes.Light.For(one).Background == Color.None);

            foreach (var other in roles)
            {
                Assert.True(
                    (Themes.Dark.For(one).Foreground == Themes.Dark.For(other).Foreground)
                    == (Themes.Light.For(one).Foreground == Themes.Light.For(other).Foreground),
                    $"{one} and {other} share a colour in one built-in and not the other.");
            }
        }
    }

    /// <summary>
    ///     The feed at 110×34, drawn headless: every cell is a pair the prototype draws — a role on the terminal's own
    ///     background, or on the band of the thing picked.
    /// </summary>
    [Fact]
    public async Task AFrameOfTheFeedIsDrawnInThePrototypesColours()
    {
        var drawn = await Frame();

        var allowed = ThePrototypes.Values
            .SelectMany(role => new[]
            {
                (Colour(role.Foreground), role.Background is null ? Color.None : Colour(role.Background)),
                (Colour(role.Foreground), Colour(role.Background ?? Band)),
            })
            .ToHashSet();

        Assert.All(drawn, cell => Assert.Contains((cell.Foreground, cell.Background), allowed));
    }

    /// <summary>
    ///     The prototype's signature in the frame: blue frames and titles, the picked post on its band, and the current
    ///     rail entry blue on a band a step lighter.
    /// </summary>
    [Fact]
    public async Task AFrameOfTheFeedCarriesTheBlueAccentAndBothBands()
    {
        var drawn = await Frame();

        Assert.Contains(drawn, cell => cell.Foreground == Colour(Blue) && cell.Background == Color.None);
        Assert.Contains(drawn, cell => cell.Background == Colour(Band));
        Assert.Contains(drawn, cell => cell.Foreground == Colour(Blue) && cell.Background == Colour(RailBand));
    }

    private static Color Colour(string hex) => ColourName.Parse(hex)!.Value;

    /// <summary>The shell opened on its first timeline at 110×34, every cell's attribute read back.</summary>
    private static async Task<List<Attribute>> Frame()
    {
        using var drawn = await DrawnShell.Of(110, 34, Themes.Dark);

        return drawn.Cells();
    }
}
