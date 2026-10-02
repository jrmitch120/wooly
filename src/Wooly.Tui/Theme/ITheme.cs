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

    /// <summary>
    ///     What to draw <paramref name="role" /> in on a row of the thing picked out: on its own background where it has
    ///     one, and on <see cref="Role.Band" />'s where it would otherwise sit on the page (#269).
    /// </summary>
    /// <remarks>
    ///     Asked of the theme rather than worked out by the view, because only the theme knows whether a role has a
    ///     background of its own — a colour compared with the page cannot tell a role sitting on the page from one
    ///     that was given the page's colour on purpose — and because a view putting an attribute together is the thing
    ///     ADR-0014 says none of them does.
    /// </remarks>
    Attribute Banded(Role role);
}
