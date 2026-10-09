using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace Wooly.Tui.Views;

/// <summary>
///     The mouse's buttons as this client takes them, put right in every mouse report before any view reads one: each
///     held from a press the terminal reported until its let-go or its click, and clicked only where it was pressed
///     (review of #372).
/// </summary>
/// <remarks>
///     <para>
///         A terminal can report the pointer merely moving as moving with a button held. Ghostty does it when it missed
///         a button's let-go — a right click whose let-go a menu or a change of focus took — and keeps doing it until its
///         window closes; macOS's terminal was found doing it with the left button (#378). Terminal.Gui believes it, so
///         it takes the button for down, and makes a click of the next report that says it is not: a wheel notch, as
///         often as not, or a press of another button. With the right button "held", every notch and every left click
///         was a right click, which is <c>esc</c> on compose and the file browser, so the wheel and a click in empty
///         space backed out of both. With the left one, a notch over the file browser's list was a click that moved the
///         cursor, and a second at the same cell a double click, which is <c>⏎</c> there; over a Media row it was a
///         click on whatever was under the pointer, its <c>x</c> included.
///     </para>
///     <para>
///         So for every button, a report of the pointer moving with it held is believed only while a press of it is, and
///         is otherwise the pointer moving and nothing more; a click Terminal.Gui made where no press was reported since
///         the last one is no click at all; and a double or triple click whose first half was one of those counts only
///         the clicks there were. Everything else goes through as reported. Asked ahead of every view and of everything
///         else that asks there, so it is laid over the application before the window is shown (<see cref="Over" />).
///     </para>
/// </remarks>
internal sealed class MouseButtons
{
    private readonly Button[] _buttons =
    [
        new(
            MouseFlags.LeftButtonPressed,
            MouseFlags.LeftButtonReleased,
            MouseFlags.LeftButtonClicked,
            MouseFlags.LeftButtonDoubleClicked,
            MouseFlags.LeftButtonTripleClicked),
        new(
            MouseFlags.MiddleButtonPressed,
            MouseFlags.MiddleButtonReleased,
            MouseFlags.MiddleButtonClicked,
            MouseFlags.MiddleButtonDoubleClicked,
            MouseFlags.MiddleButtonTripleClicked),
        new(
            MouseFlags.RightButtonPressed,
            MouseFlags.RightButtonReleased,
            MouseFlags.RightButtonClicked,
            MouseFlags.RightButtonDoubleClicked,
            MouseFlags.RightButtonTripleClicked),
    ];

    /// <summary>
    ///     Puts every report <paramref name="application" /> raises right before anything else reads it — which is why
    ///     it is laid over the application before the window is shown, the window's views asking there as they start.
    /// </summary>
    public static void Over(IApplication application)
    {
        var buttons = new MouseButtons();

        application.Mouse.MouseEvent += (_, mouse) => buttons.Correct(mouse);
    }

    /// <summary>
    ///     <paramref name="mouse" /> put right: its flags as this client takes them, or handled, so that nothing reads it,
    ///     where it is a click nobody made.
    /// </summary>
    public void Correct(Mouse mouse)
    {
        foreach (var button in _buttons)
        {
            button.Correct(mouse);
        }
    }

    /// <summary>One button: the flags Terminal.Gui reports it by, and what this client believes it did.</summary>
    private sealed class Button(
        MouseFlags pressed,
        MouseFlags released,
        MouseFlags clicked,
        MouseFlags doubleClicked,
        MouseFlags tripleClicked)
    {
        /// <summary>Every click of the button, one or several.</summary>
        private readonly MouseFlags _clicks = clicked | doubleClicked | tripleClicked;

        /// <summary>Whether a press was reported and neither its let-go nor its click has been since.</summary>
        private bool _held;

        /// <summary>Whether a press was reported since the last click let through, which is what a click needs to be one.</summary>
        private bool _pressed;

        /// <summary>How many clicks of the run Terminal.Gui is counting were let through, up to three.</summary>
        private int _run;

        public void Correct(Mouse mouse)
        {
            var flags = mouse.Flags;

            if (flags.HasFlag(pressed))
            {
                if (!flags.HasFlag(MouseFlags.PositionReport))
                {
                    _held = true;
                    _pressed = true;
                }
                else if (!_held)
                {
                    mouse.Flags = flags & ~pressed;
                }

                return;
            }

            if (flags.HasFlag(released))
            {
                _held = false;

                return;
            }

            if ((flags & _clicks) == 0)
            {
                return;
            }

            // A click puts the button down, its let-go reported or not.
            _held = false;

            if (!_pressed)
            {
                mouse.Handled = true;
                _run = 0;

                return;
            }

            _pressed = false;
            _run = flags.HasFlag(clicked) ? 1 : Math.Min(_run + 1, 3);

            mouse.Flags = (flags & ~_clicks) | _run switch
            {
                1 => clicked,
                2 => doubleClicked,
                _ => tripleClicked,
            };
        }
    }
}
