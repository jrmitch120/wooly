using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Wooly.Tui.Theme;

/// <summary>
///     Answers a <see cref="Role" /> with the attribute to draw it in. The only thing in the TUI that holds colours;
///     every view names roles and this resolves them (ADR-0014).
/// </summary>
/// <remarks>
///     One theme ships behind this seam for now. The <c>[themes.*]</c> config, the light variant and a user's own
///     theme are #46 — this ticket introduces the seam and nothing else, which is what lets #46 be a new
///     implementation rather than a change to every screen.
/// </remarks>
public interface ITheme
{
    /// <summary>What to draw <paramref name="role" /> in.</summary>
    Attribute For(Role role);
}
