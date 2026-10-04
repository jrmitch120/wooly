using Terminal.Gui.Drawing;
using Wooly.Core.Configuration;
using Wooly.Core.Errors;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Theme;

/// <summary>
///     The themes built in, which one this run draws in, and how a theme somebody wrote in the config file is read
///     against them (#46).
/// </summary>
/// <remarks>
///     Terminal.Gui's own <c>ConfigurationManager</c> is deliberately not adopted. Story 5 promises one human-readable
///     config file, and its JSON is a second one — worse, a stray <c>~/.tui-config.json</c> left by an unrelated
///     Terminal.Gui app would restyle this client without its user having asked for anything.
/// </remarks>
public static class Themes
{
    /// <summary>
    ///     The convention a terminal announces "send me no colour" by. Honoured by asking the environment rather than
    ///     the driver: the answer is wanted before a screen is built, and it is the same answer either way.
    /// </summary>
    private const string NoColorVariable = "NO_COLOR";

    /// <summary>The terminal that cannot draw colour at all names itself this.</summary>
    private const string DumbTerminal = "dumb";

    private const string DarkName = "dark";
    private const string LightName = "light";

    /// <summary>
    ///     The roles drawn on something other than the page: the ones told apart by the band they are drawn in rather
    ///     than by the text on them.
    /// </summary>
    /// <remarks>
    ///     <see cref="Role.Selection" /> is not one of them. Its <c>▌</c> sits on <see cref="Role.Band" /> with the rest
    ///     of the picked row, so a theme naming <c>band</c> alone moves the whole row, and the band and the page are
    ///     themed apart (#269).
    /// </remarks>
    private static readonly Dictionary<Role, string> DarkBands = new()
    {
        // A lift off black rather than a shade of grey: as dark as it can go and still read as a band.
        [Role.Band] = "#16171c",
        [Role.RailCurrent] = "#1f2128",
        [Role.RailCursor] = "#313244",

        // Catppuccin's Surface2: a selection lifted well off the page, where the band is barely there (#316).
        [Role.SelectedText] = "#585b70",
    };

    private static readonly Dictionary<Role, string> LightBands = new()
    {
        [Role.Band] = "#e9eaef",
        [Role.RailCurrent] = "#dfe1e8",
        [Role.RailCursor] = "#ccd0da",
        [Role.SelectedText] = "#acb0be",
    };

    // Every built-in draws on the terminal's own background, so the app meets the terminal's padding with no seam
    // (ADR-0021). Only the bands above bring a background of their own.
    //
    // The panels palette, tuned in the prototype ADR-0021 chose (skin B2 on prototype/tui-skins): Catppuccin Mocha's accents, with blue the one accent — the frame
    // you are in, the titles, the rail's current entry and the gauge — and the picked thing's mark a near-white above
    // the body text (#273). The prototype told yours from anybody's boost and favorite by weight alone, and a theme
    // here is colours, so the two share one: the glyph carries it, as it does without colour.
    private static readonly Palette DarkPalette = Palette.Of(
        ColourName.Default,
        new Dictionary<Role, string>
        {
            [Role.Body] = "#cdd6f4",
            [Role.Hashtag] = "#94e2d5",
            [Role.Mention] = "#cba6f7",
            [Role.Link] = "#89b4fa",
            [Role.Muted] = "#6c7086",
            [Role.BylineName] = "#cdd6f4",
            [Role.BylineHandle] = "#74c7ec",
            [Role.Audience] = "#6c7086",
            [Role.ContentWarning] = "#fab387",
            [Role.Media] = "#94e2d5",
            [Role.Poll] = "#cba6f7",
            [Role.ReferencePicked] = "#f2f0f7",
            [Role.Boost] = "#a6e3a1",
            [Role.BoostMine] = "#a6e3a1",
            [Role.Favorite] = "#f9e2af",
            [Role.FavoriteMine] = "#f9e2af",
            [Role.Replies] = "#cba6f7",
            [Role.Selection] = "#f2f0f7",
            [Role.Band] = "#f2f0f7",
            [Role.SelectedText] = "#cdd6f4",
            [Role.Rail] = "#a6adc8",
            [Role.RailCurrent] = "#89b4fa",
            [Role.RailCursor] = "#cdd6f4",
            [Role.RailUnread] = "#fab387",
            [Role.Quota] = "#6c7086",
            [Role.QuotaLow] = "#f9e2af",
            [Role.Gauge] = "#89b4fa",
            [Role.GaugeEmpty] = "#45475a",
            [Role.Chrome] = "#585b70",
            [Role.Key] = "#74c7ec",
            [Role.PanelBorder] = "#585b70",
            [Role.PanelBorderActive] = "#89b4fa",
            [Role.PanelTitle] = "#89b4fa",
            [Role.Loading] = "#6c7086",
            [Role.Spinner] = "#fab387",
            [Role.Destructive] = "#f38ba8",
            [Role.Error] = "#f38ba8",
        },
        DarkBands);

    // The same structure in Catppuccin Latte, judged on a light terminal: each role takes the Latte twin of its Mocha
    // colour, the greys a step darker where Latte's own would wash out on white, and the pick mark a near-black below
    // the body text.
    private static readonly Palette LightPalette = Palette.Of(
        ColourName.Default,
        new Dictionary<Role, string>
        {
            [Role.Body] = "#4c4f69",
            [Role.Hashtag] = "#179299",
            [Role.Mention] = "#8839ef",
            [Role.Link] = "#1e66f5",
            [Role.Muted] = "#7c7f93",
            [Role.BylineName] = "#4c4f69",
            [Role.BylineHandle] = "#209fb5",
            [Role.Audience] = "#7c7f93",
            [Role.ContentWarning] = "#fe640b",
            [Role.Media] = "#179299",
            [Role.Poll] = "#8839ef",
            [Role.ReferencePicked] = "#100e18",
            [Role.Boost] = "#40a02b",
            [Role.BoostMine] = "#40a02b",
            [Role.Favorite] = "#df8e1d",
            [Role.FavoriteMine] = "#df8e1d",
            [Role.Replies] = "#8839ef",
            [Role.Selection] = "#100e18",
            [Role.Band] = "#100e18",
            [Role.SelectedText] = "#4c4f69",
            [Role.Rail] = "#6c6f85",
            [Role.RailCurrent] = "#1e66f5",
            [Role.RailCursor] = "#4c4f69",
            [Role.RailUnread] = "#fe640b",
            [Role.Quota] = "#7c7f93",
            [Role.QuotaLow] = "#df8e1d",
            [Role.Gauge] = "#1e66f5",
            [Role.GaugeEmpty] = "#bcc0cc",
            [Role.Chrome] = "#acb0be",
            [Role.Key] = "#209fb5",
            [Role.PanelBorder] = "#acb0be",
            [Role.PanelBorderActive] = "#1e66f5",
            [Role.PanelTitle] = "#1e66f5",
            [Role.Loading] = "#7c7f93",
            [Role.Spinner] = "#fe640b",
            [Role.Destructive] = "#d20f39",
            [Role.Error] = "#d20f39",
        },
        LightBands);

    /// <summary>The built-in for a dark terminal.</summary>
    public static ITheme Dark => DarkPalette;

    /// <summary>The built-in for a light one. The same vocabulary, said in colours that survive being drawn on paper.</summary>
    public static ITheme Light => LightPalette;

    /// <summary>
    ///     Every role in one pair — and selected text in that pair reversed — for a terminal that has said it wants no
    ///     colour. Not a degraded theme but the absence of one: every state the TUI shows carries a glyph before it
    ///     carries a colour (ADR-0014), so what is left here still says everything the shell has to say.
    /// </summary>
    public static ITheme Plain { get; } = new PlainTheme();

    /// <summary>Which theme this terminal should be drawn in, given what the config file asks for.</summary>
    /// <remarks>
    ///     The file is read either way. A terminal with no colour draws none of it — no theme survives <c>NO_COLOR</c>
    ///     — but a role misspelled in it is still said out loud, so that a typo is reported to everybody rather than
    ///     only to the readers whose terminals happen to have colour.
    /// </remarks>
    /// <exception cref="ConfigurationException">The file names a theme, a role or a colour this client cannot read.</exception>
    public static ITheme ForCurrentTerminal(WoolyConfig config, string configFile)
    {
        var chosen = Chosen(config, configFile);

        return HasColour() ? chosen : Plain;
    }

    /// <summary>
    ///     The theme the config file chose, whatever the terminal can draw: the built-in of that name, or the one
    ///     somebody wrote under it, read against the built-in it is closest to.
    /// </summary>
    /// <exception cref="ConfigurationException">The file names a theme, a role or a colour this client cannot read.</exception>
    public static ITheme Chosen(WoolyConfig config, string configFile)
    {
        // Every theme in the file, not only the one in use. A role misspelled in a theme somebody keeps for switching
        // to is a mistake in this file the day it is written, and the moment they switch is the worst moment to be
        // told about it.
        var written = config.Themes.ToDictionary(
            theme => theme.Key,
            theme => Read(theme.Key, theme.Value, configFile),
            StringComparer.Ordinal);

        var name = config.Theme ?? DarkName;

        if (written.TryGetValue(name, out var theirs))
        {
            return theirs;
        }

        return BuiltIn(name)
               ?? throw new ConfigurationException(
                   configFile,
                   $"theme '{name}' is not one this client has. Name a theme written in this file, or one of the "
                   + $"built-in '{DarkName}' and '{LightName}'.");
    }

    /// <summary>One theme as written, over the built-in it is read against.</summary>
    private static Palette Read(string name, ThemeConfig theme, string configFile)
    {
        var background = Colour($"the background of theme '{name}'", theme.Background, configFile);
        var named = new Dictionary<Role, (Color? Foreground, Color? Background)>();

        foreach (var (key, colour) in theme.Roles)
        {
            // A role nobody has heard of is a typo, and a typo that paints nothing and says nothing leaves its author
            // waiting for a colour that was never going to arrive.
            var role = RoleName.For(key)
                       ?? throw new ConfigurationException(
                           configFile,
                           $"theme '{name}' names a role called '{key}', which this client has none of. The roles "
                           + "are listed in docs/tui-shell.md.");

            named[role] = (
                Colour($"'{key}' in theme '{name}'", colour.Foreground, configFile),
                Colour($"the background of '{key}' in theme '{name}'", colour.Background, configFile));
        }

        return Beneath(name, background).Overlaid(background, named);
    }

    /// <summary>
    ///     The built-in a theme is read against: the one whose brightness its page matches, or — for a theme that
    ///     names no page, or names the terminal's own, which has no brightness this client can know — the one it
    ///     shares a name with.
    /// </summary>
    /// <remarks>
    ///     The page comes first, and beats the name, because the failure it is guarding against is the only one a
    ///     fallback must never produce: light text on light paper. A theme that names a light background is a light
    ///     theme whatever it is called, <c>[themes.dark]</c> included.
    /// </remarks>
    private static Palette Beneath(string name, Color? background) => background is { } page && page != Color.None
        ? IsLight(page) ? LightPalette : DarkPalette
        : BuiltIn(name) ?? DarkPalette;

    private static Palette? BuiltIn(string name) => name switch
    {
        DarkName => DarkPalette,
        LightName => LightPalette,
        _ => null,
    };

    /// <summary>How bright a colour reads, weighted the way an eye weights the three channels.</summary>
    private static bool IsLight(Color colour) =>
        ((0.2126 * colour.R) + (0.7152 * colour.G) + (0.0722 * colour.B)) / 255 > 0.5;

    /// <summary>
    ///     One colour as somebody wrote it, or nothing where they wrote none. <paramref name="what" /> is what the
    ///     colour was for, said in the error so that a file with a dozen colours in it says which one.
    /// </summary>
    private static Color? Colour(string what, string? written, string configFile)
    {
        if (written is null)
        {
            return null;
        }

        return ColourName.Parse(written)
               ?? throw new ConfigurationException(configFile, $"{what}: {ColourName.Rejection(written)}");
    }

    /// <summary>Whether this terminal wants colour at all.</summary>
    private static bool HasColour() =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable(NoColorVariable))
        && !string.Equals(Environment.GetEnvironmentVariable("TERM"), DumbTerminal, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     One pair for everything. Terminal.Gui's own default, which is what its drivers write where the terminal has
    ///     said it has no colour to write.
    /// </summary>
    /// <remarks>
    ///     All but selected text, which is that pair reversed: a selection has no glyph to carry it, and a terminal
    ///     with no colour can still swap the two it has (#316).
    /// </remarks>
    private sealed class PlainTheme : ITheme
    {
        private static readonly Attribute Reversed =
            new(Attribute.Default.Foreground, Attribute.Default.Background, TextStyle.Reverse);

        public Attribute For(Role role) => role == Role.SelectedText ? Reversed : Attribute.Default;

        public Attribute Banded(Role role) => For(role);

        public bool DrawsColour => false;
    }
}
