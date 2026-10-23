using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Views;

/// <summary>
///     What the fields of every view laid over a draft — compose's (#365), the edit public profile form's (#406), the
///     description editor's (#377, #430) — do the same way with a question open on the status row and with the mouse,
///     ahead of the views under the pointer: a key answers the question where there is one, a right click is the
///     draft's <c>esc</c> wherever it lands, and a press over a field while a question is open goes to the question
///     rather than the field (#373). The rules are shared here and the widgets stay each view's own (ADR-0030).
/// </summary>
/// <remarks>
///     The mouse is asked about where the terminal's mouse events arrive, ahead of every view under the pointer, from
///     when the view the fields are laid in is running until it is disposed: a field would otherwise keep a right click
///     to itself, and an open list could not close on a click that lands on another view.
/// </remarks>
internal sealed class DraftFields
{
    private readonly View _view;

    private readonly Shell.Shell _shell;

    private readonly Func<bool> _inFront;

    private readonly Func<IEnumerable<View>> _fields;

    private readonly Func<Mouse, bool> _closes;

    /// <summary>Where the terminal's mouse events arrive, once the view is running; asked ahead of the views.</summary>
    private IMouse? _mouse;

    /// <param name="view">
    ///     The view the fields are laid in, from whose running until whose disposal the mouse is asked about.
    /// </param>
    /// <param name="shell">Whose question, and whose way out.</param>
    /// <param name="inFront">
    ///     Whether the draft is the screen in front, which is where a right click is its <c>esc</c>.
    /// </param>
    /// <param name="fields">
    ///     The view's fields, which take the pointer before the window does while they are showing.
    /// </param>
    /// <param name="closes">
    ///     What else the view spends a mouse event on ahead of the views once these rules have let it by: closing an
    ///     open list on a click outside it (#335).
    /// </param>
    public DraftFields(
        View view,
        Shell.Shell shell,
        Func<bool> inFront,
        Func<IEnumerable<View>> fields,
        Func<Mouse, bool>? closes = null)
    {
        _view = view;
        _shell = shell;
        _inFront = inFront;
        _fields = fields;
        _closes = closes ?? (_ => false);

        view.Initialized += (_, _) =>
        {
            _mouse = view.App?.Mouse;

            if (_mouse is not null)
            {
                _mouse.MouseEvent += AheadOfTheViews;
            }
        };

        view.Disposing += (_, _) =>
        {
            if (_mouse is not null)
            {
                _mouse.MouseEvent -= AheadOfTheViews;
                _mouse = null;
            }
        };
    }

    /// <summary>
    ///     <paramref name="key" /> as the answer to a question the shell has open, where it has one (<see cref="Answered(ShellKey?)" />).
    /// </summary>
    public bool Answered(Key key) => Answered(ShellKeys.Of(key));

    /// <summary>
    ///     <paramref name="pressed" /> as the answer to a question the shell has open on the status row — <c>y</c>
    ///     agrees, anything else keeps — where it has one, and nothing where it has none.
    /// </summary>
    /// <returns>Whether there was a question, and the press was spent on it.</returns>
    public bool Answered(ShellKey? pressed) => _shell.StatusRow.Answered(pressed);

    /// <summary>
    ///     A mouse event wherever it landed, before the view under the pointer sees it: a right click while the draft
    ///     is in front (<see cref="RightClicked" />), or a press over a field while a question is open
    ///     (<see cref="Withheld" />), which the draft takes; then whatever the view closes on it; and left alone
    ///     otherwise.
    /// </summary>
    private void AheadOfTheViews(object? sender, Mouse mouse)
    {
        if (!mouse.Handled)
        {
            mouse.Handled = RightClicked(mouse) || Withheld(mouse) || _closes(mouse);
        }
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
        if (!_inFront() || ShellKeys.Of(mouse) is not { } pressed)
        {
            return false;
        }

        if (!Answered(pressed))
        {
            _shell.Back();
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
        if (!Over(mouse.ScreenPosition))
        {
            return false;
        }

        // A notch as the window reads one, so that a sideways one — which carries a vertical one's bit — is no answer.
        // Anything else over a field is only kept from it, and only while there is a question to keep it from.
        var answers = mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked) || ShellWindow.Notched(mouse) is not null;

        if (answers ? !_shell.StatusRow.Answered(press: null) : _shell.StatusRow.Shown is not Shown.Asking)
        {
            return false;
        }

        return mouse.Flags != MouseFlags.None && mouse.Flags != MouseFlags.PositionReport;
    }

    /// <summary>Whether <paramref name="at" /> is over one of the view's fields as they are showing.</summary>
    private bool Over(Point at) =>
        _view.Visible && _fields().Any(field => field.Visible && field.FrameToScreen().Contains(at));
}
