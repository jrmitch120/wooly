using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Theme;

/// <summary>
///     Answers a <see cref="Role" /> with the attribute to draw it in. The only thing in the TUI that holds colours;
///     every view names roles and this resolves them (ADR-0014).
/// </summary>
/// <remarks>
///     Three kinds of thing answer it: the built-ins, a theme somebody wrote in the config file, and the single pair a
///     terminal that has asked for no colour gets. Because every screen names roles and nothing else, all three
///     arrived without a screen changing.
/// </remarks>
public interface ITheme
{
    /// <summary>What to draw <paramref name="role" /> in.</summary>
    Attribute For(Role role);
}
