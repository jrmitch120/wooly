using System.Drawing;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Views;

/// <summary>
///     Where a view laid over a draft — compose's, the edit public profile form's, the description editor's — puts
///     each of its widgets: on the rectangle its screen works out in the view's viewport while that screen is on top,
///     and nowhere otherwise (ADR-0030). The screen says where; this only binds a widget to what it says.
/// </summary>
/// <typeparam name="TScreen">The screen the widgets are laid out by.</typeparam>
/// <param name="over">The view the widgets are laid in, whose viewport is the room the screen lays them out in.</param>
/// <param name="shell">Whose screen is on top.</param>
internal sealed class Placement<TScreen>(View over, Shell.Shell shell)
    where TScreen : Screen
{
    /// <summary>
    ///     <paramref name="widget" />, hidden until it is shown, and laid wherever <paramref name="at" /> says the
    ///     screen on top puts it in the view's viewport — nowhere while that screen is not on top, or the view has no
    ///     room yet.
    /// </summary>
    public T Placed<T>(T widget, Func<TScreen, Size, Rectangle> at)
        where T : View
    {
        Rectangle Laid() =>
            over.Viewport.Width > 0 && shell.Screen is TScreen screen
                ? at(screen, over.Viewport.Size)
                : Rectangle.Empty;

        widget.X = Pos.Func(_ => Laid().X);
        widget.Y = Pos.Func(_ => Laid().Y);
        widget.Width = Dim.Func(_ => Laid().Width);
        widget.Height = Dim.Func(_ => Laid().Height);
        widget.Visible = false;

        return widget;
    }
}
