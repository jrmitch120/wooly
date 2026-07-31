using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Theme;

/// <summary>
///     The themes built in, and which one a terminal gets. Two of them for now: one for a dark terminal, and one for a
///     terminal that has said it wants no colour at all.
/// </summary>
public static class Themes
{
    /// <summary>
    ///     The convention a terminal announces "send me no colour" by. Honoured by asking the environment rather than
    ///     the driver: the answer is wanted before a screen is built, and it is the same answer either way.
    /// </summary>
    private const string NoColorVariable = "NO_COLOR";

    /// <summary>The terminal that cannot draw colour at all names itself this.</summary>
    private const string DumbTerminal = "dumb";

    /// <summary>The one built-in theme, for a dark terminal.</summary>
    public static ITheme Dark { get; } = new PaletteTheme(new Dictionary<Role, Attribute>
    {
        [Role.Body] = Pair("#d5d2e0"),
        [Role.Muted] = Pair("#7c7891"),
        [Role.BylineName] = Pair("#f2f0f7"),
        [Role.BylineHandle] = Pair("#8fa8ff"),
        [Role.Audience] = Pair("#7c7891"),
        [Role.ContentWarning] = Pair("#e0af68"),
        [Role.Media] = Pair("#8fa8ff"),
        [Role.Poll] = Pair("#8fa8ff"),
        [Role.Boost] = Pair("#6fcf97"),
        [Role.BoostMine] = Pair("#9ef2b8"),
        [Role.Favorite] = Pair("#c58fe8"),
        [Role.FavoriteMine] = Pair("#e0b6ff"),

        // The one role that sets its own background, because a selected row is told apart by the row and not by the
        // text on it.
        [Role.Selection] = new Attribute(new Color("#f2f0f7"), new Color("#2a2942")),
        [Role.Rail] = Pair("#d5d2e0"),
        [Role.RailCurrent] = new Attribute(new Color("#f2f0f7"), new Color("#2a2942")),
        [Role.RailUnread] = Pair("#ff7a93"),
        [Role.Quota] = Pair("#7c7891"),
        [Role.QuotaLow] = Pair("#ff7a93"),
        [Role.Chrome] = Pair("#7c7891"),
        [Role.Loading] = Pair("#5c5872"),
        [Role.Destructive] = Pair("#ff7a93"),
        [Role.Error] = Pair("#ff7a93"),
    });

    /// <summary>
    ///     Every role in the terminal's own default pair, for a terminal that reports no colour. Not a degraded theme
    ///     but the absence of one: every state the TUI shows carries a glyph before it carries a colour (ADR-0014), so
    ///     what is left here still says everything the shell has to say.
    /// </summary>
    public static ITheme Plain { get; } = new PlainTheme();

    /// <summary>Which theme this terminal should be drawn in.</summary>
    public static ITheme ForCurrentTerminal() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(NoColorVariable)) ||
        string.Equals(Environment.GetEnvironmentVariable("TERM"), DumbTerminal, StringComparison.OrdinalIgnoreCase)
            ? Plain
            : Dark;

    /// <summary>
    ///     A foreground on the theme's own background. Written as one call because a role that does not care about its
    ///     background should not have to name one, and every role naming its own is how two of them come to disagree
    ///     about what the page is.
    /// </summary>
    private static Attribute Pair(string foreground) => new(new Color(foreground), new Color("#12111a"));

    /// <summary>A theme that is a table of roles, which is what every theme is once #46 can read one out of TOML.</summary>
    private sealed class PaletteTheme(IReadOnlyDictionary<Role, Attribute> palette) : ITheme
    {
        public Attribute For(Role role) => palette.TryGetValue(role, out var attribute)
            ? attribute

            // A role the built-in theme forgot is a defect in this file rather than something to paint grey and hope
            // nobody notices — which is precisely the failure a role table exists to make impossible.
            : throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                $"The built-in theme answers no role called '{RoleName.Of(role)}'.");
    }

    private sealed class PlainTheme : ITheme
    {
        public Attribute For(Role role) => Attribute.Default;
    }
}
