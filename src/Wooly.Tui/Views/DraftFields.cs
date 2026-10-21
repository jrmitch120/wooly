using System.Drawing;
using Terminal.Gui.Input;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Views;

/// <summary>
///     What the fields of a draft's view — compose's (#365), the edit public profile form's (#406) — do the same way
///     with a question open on the status row and with the mouse, ahead of the views under the pointer: a key answers
///     the question where there is one, a right click is the draft's <c>esc</c> wherever it lands, and a press over a
///     field while a question is open goes to the question rather than the field (#373).
/// </summary>
/// <param name="shell">Whose question, and whose way out.</param>
/// <param name="inFront">Whether the draft is the screen in front, which is where a right click is its <c>esc</c>.</param>
/// <param name="over">Whether a point on the screen is over one of the draft's fields as they are showing.</param>
internal sealed class DraftFields(Shell.Shell shell, Func<bool> inFront, Func<Point, bool> over)
{
    /// <summary>
    ///     A mouse event the draft takes before the view under the pointer sees it: a right click while the draft is in
    ///     front (<see cref="RightClicked" />), or a press over a field while a question is open (<see cref="Withheld" />).
    /// </summary>
    /// <returns>Whether it was one of those, and spent.</returns>
    public bool Spent(Mouse mouse) => RightClicked(mouse) || Withheld(mouse);

    /// <summary>
    ///     <paramref name="key" /> as the answer to a question the shell has open, where it has one (<see cref="Answered(ShellKey?)" />).
    /// </summary>
    public bool Answered(Key key) => Answered(ShellKeys.Of(key));

    /// <summary>
    ///     <paramref name="pressed" /> as the answer to a question the shell has open on the status row — <c>y</c>
    ///     agrees, anything else keeps — where it has one, and nothing where it has none.
    /// </summary>
    /// <returns>Whether there was a question, and the press was spent on it.</returns>
    public bool Answered(ShellKey? pressed)
    {
        if (shell.Asking is null)
        {
            return false;
        }

        _ = shell.Answer(pressed);

        return true;
    }

    /// <summary>
    ///     A right click with the draft in front: <c>esc</c>, wherever it lands — over a field, which would otherwise keep
    ///     it to itself, as anywhere else. It asks before a touched draft is thrown away and, pressed again on that
    ///     question, keeps the draft, the way <c>esc</c> does (#373). No list under the pointer closes or picks for it,
    ///     and an editor's own context menu never opens.
    /// </summary>
    /// <returns>Whether it was one, and spent.</returns>
    private bool RightClicked(Mouse mouse)
    {
        if (!inFront() || ShellKeys.Of(mouse) is not { } pressed)
        {
            return false;
        }

        if (!Answered(pressed))
        {
            shell.Back();
        }

        return true;
    }

    /// <summary>
    ///     A button or the wheel over one of the draft's fields while a question is open on the status row: kept from the
    ///     field, so no caret moves and nothing scrolls behind the question, and a click or a notch declines it, as they
    ///     do anywhere else in the shell (open questions win, #373). Elsewhere the window declines it itself.
    /// </summary>
    /// <returns>Whether the event was over a field and spent.</returns>
    private bool Withheld(Mouse mouse)
    {
        if (shell.Asking is null || !over(mouse.ScreenPosition))
        {
            return false;
        }

        // A notch as the window reads one, so that a sideways one — which carries a vertical one's bit — is no answer.
        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked) || ShellWindow.Notched(mouse) is not null)
        {
            _ = shell.Answer(pressed: null);
        }

        return mouse.Flags != MouseFlags.None && mouse.Flags != MouseFlags.PositionReport;
    }
}
