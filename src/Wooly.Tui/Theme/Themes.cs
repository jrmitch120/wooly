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

    /// <summary>The two roles told apart by the band they are drawn in rather than by the text on them.</summary>
    private static readonly Dictionary<Role, string> DarkBands = new()
    {
        [Role.Selection] = "#2a2942",
        [Role.RailCurrent] = "#2a2942",
    };

    private static readonly Dictionary<Role, string> LightBands = new()
    {
        [Role.Selection] = "#dcd9e8",
        [Role.RailCurrent] = "#dcd9e8",
    };

    private static readonly Palette DarkPalette = Palette.Of(
        "#12111a",
        new Dictionary<Role, string>
        {
            [Role.Body] = "#d5d2e0",
            [Role.Hashtag] = "#6fcf97",
            [Role.Mention] = "#e0af68",
            [Role.Link] = "#8fa8ff",
            [Role.Muted] = "#7c7891",
            [Role.BylineName] = "#f2f0f7",
            [Role.BylineHandle] = "#8fa8ff",
            [Role.Audience] = "#7c7891",
            [Role.ContentWarning] = "#e0af68",
            [Role.Media] = "#8fa8ff",
            [Role.Poll] = "#8fa8ff",
            [Role.Boost] = "#6fcf97",
            [Role.BoostMine] = "#9ef2b8",
            [Role.Favorite] = "#c58fe8",
            [Role.FavoriteMine] = "#e0b6ff",
            [Role.Selection] = "#f2f0f7",
            [Role.Rail] = "#d5d2e0",
            [Role.RailCurrent] = "#f2f0f7",
            [Role.RailUnread] = "#ff7a93",
            [Role.Quota] = "#7c7891",
            [Role.QuotaLow] = "#ff7a93",
            [Role.Chrome] = "#7c7891",
            [Role.Loading] = "#5c5872",
            [Role.Destructive] = "#ff7a93",
            [Role.Error] = "#ff7a93",
        },
        DarkBands);

    private static readonly Palette LightPalette = Palette.Of(
        "#faf9fb",
        new Dictionary<Role, string>
        {
            [Role.Body] = "#23212e",
            [Role.Hashtag] = "#1a7a4a",
            [Role.Mention] = "#8a5a00",
            [Role.Link] = "#2848c8",
            [Role.Muted] = "#6a6780",
            [Role.BylineName] = "#100e18",
            [Role.BylineHandle] = "#2848c8",
            [Role.Audience] = "#6a6780",
            [Role.ContentWarning] = "#8a5a00",
            [Role.Media] = "#2848c8",
            [Role.Poll] = "#2848c8",
            [Role.Boost] = "#1a7a4a",
            [Role.BoostMine] = "#0d5c34",
            [Role.Favorite] = "#7b35b5",
            [Role.FavoriteMine] = "#5c2090",
            [Role.Selection] = "#100e18",
            [Role.Rail] = "#23212e",
            [Role.RailCurrent] = "#100e18",
            [Role.RailUnread] = "#b3123f",
            [Role.Quota] = "#6a6780",
            [Role.QuotaLow] = "#b3123f",
            [Role.Chrome] = "#6a6780",
            [Role.Loading] = "#9b98ad",
            [Role.Destructive] = "#b3123f",
            [Role.Error] = "#b3123f",
        },
        LightBands);

    /// <summary>The built-in for a dark terminal.</summary>
    public static ITheme Dark => DarkPalette;

    /// <summary>The built-in for a light one. The same vocabulary, said in colours that survive being drawn on paper.</summary>
    public static ITheme Light => LightPalette;

    /// <summary>
    ///     Every role in one pair, for a terminal that has said it wants no colour. Not a degraded theme but the
    ///     absence of one: every state the TUI shows carries a glyph before it carries a colour (ADR-0014), so what is
    ///     left here still says everything the shell has to say.
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
        var background = theme.Background is null
            ? (Color?)null
            : ColourName.Parse(theme.Background)
              ?? throw new ConfigurationException(
                  configFile,
                  $"theme '{name}' has a background this client cannot read: {ColourName.Rejection(theme.Background)}");

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
                Colour(name, key, colour.Foreground, configFile),
                Colour(name, key, colour.Background, configFile));
        }

        return Beneath(name, background).Overlaid(background, named);
    }

    /// <summary>
    ///     The built-in a theme is read against: the one it shares a name with, or — for a theme with a name of its
    ///     own — the one of its own brightness.
    /// </summary>
    /// <remarks>
    ///     Its brightness is its background's, since that is the only thing a theme says about the page it is drawn
    ///     on. Without this rule a theme naming a light page and nothing else would be light text on light paper,
    ///     which is the one thing a fallback must not produce.
    /// </remarks>
    private static Palette Beneath(string name, Color? background) =>
        BuiltIn(name) ?? (background is { } page && IsLight(page) ? LightPalette : DarkPalette);

    private static Palette? BuiltIn(string name) => name switch
    {
        DarkName => DarkPalette,
        LightName => LightPalette,
        _ => null,
    };

    /// <summary>How bright a colour reads, weighted the way an eye weights the three channels.</summary>
    private static bool IsLight(Color colour) =>
        ((0.2126 * colour.R) + (0.7152 * colour.G) + (0.0722 * colour.B)) / 255 > 0.5;

    private static Color? Colour(string theme, string role, string? written, string configFile)
    {
        if (written is null)
        {
            return null;
        }

        return ColourName.Parse(written)
               ?? throw new ConfigurationException(
                   configFile,
                   $"theme '{theme}' gives '{role}' a colour this client cannot read: {ColourName.Rejection(written)}");
    }

    /// <summary>Whether this terminal wants colour at all.</summary>
    private static bool HasColour() =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable(NoColorVariable))
        && !string.Equals(Environment.GetEnvironmentVariable("TERM"), DumbTerminal, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     One pair for everything. Terminal.Gui's own default, which is what its drivers write where the terminal has
    ///     said it has no colour to write.
    /// </summary>
    private sealed class PlainTheme : ITheme
    {
        public Attribute For(Role role) => Attribute.Default;
    }
}
