using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace Wooly.Tui.Views;

/// <summary>
///     The left button as this client takes it, put right in every mouse report before any view reads one: held from a
///     press the terminal reported until its let-go or its click, and clicked only where it was pressed (review of
///     #372).
/// </summary>
/// <remarks>
///     <para>
///         Some terminals report the pointer moving with no button held in the very words they report it moving with
///         the left button held — macOS's was found to (#378). Terminal.Gui believes them, so it takes the button for
///         down, and makes a click of the next report that says it is not: a wheel notch, as often as not. A notch over
///         the file browser's list after the pointer drifted was a click that moved the cursor, and a second at the same
///         cell a double click, which is <c>⏎</c> there, so the wheel attached a file and left the browser; over a Media
///         row it was a click on whatever was under the pointer, its <c>x</c> included.
///     </para>
///     <para>
///         So a report of the pointer moving with the button held is believed only while a press is, and is otherwise
///         the pointer moving and nothing more; a click Terminal.Gui made where no press was reported since the last
///         one is no click at all; and a double or triple click whose first half was one of those counts only the
///         clicks there were. Everything else goes through as reported. Asked ahead of every view and of everything else
///         that asks there, so it is laid over the application before the window is shown (<see cref="Over" />).
///     </para>
/// </remarks>
internal sealed class LeftButton
{
    /// <summary>Every click of the left button, one or several.</summary>
    private const MouseFlags Clicks =
        MouseFlags.LeftButtonClicked | MouseFlags.LeftButtonDoubleClicked | MouseFlags.LeftButtonTripleClicked;

    /// <summary>Whether a press was reported and neither its let-go nor its click has been since.</summary>
    private bool _held;

    /// <summary>Whether a press was reported since the last click let through, which is what a click needs to be one.</summary>
    private bool _pressed;

    /// <summary>How many clicks of the run Terminal.Gui is counting were let through, up to three.</summary>
    private int _run;

    /// <summary>
    ///     Puts every report <paramref name="application" /> raises right before anything else reads it — which is why
    ///     it is laid over the application before the window is shown, the window's views asking there as they start.
    /// </summary>
    public static void Over(IApplication application)
    {
        var button = new LeftButton();

        application.Mouse.MouseEvent += (_, mouse) => button.Correct(mouse);
    }

    /// <summary>
    ///     <paramref name="mouse" /> put right: its flags as this client takes them, or handled, so that nothing reads it,
    ///     where it is a click nobody made.
    /// </summary>
    public void Correct(Mouse mouse)
    {
        var flags = mouse.Flags;

        if (flags.HasFlag(MouseFlags.LeftButtonPressed))
        {
            if (!flags.HasFlag(MouseFlags.PositionReport))
            {
                _held = true;
                _pressed = true;
            }
            else if (!_held)
            {
                mouse.Flags = flags & ~MouseFlags.LeftButtonPressed;
            }

            return;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonReleased))
        {
            _held = false;

            return;
        }

        if ((flags & Clicks) == 0)
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
        _run = flags.HasFlag(MouseFlags.LeftButtonClicked) ? 1 : Math.Min(_run + 1, 3);

        mouse.Flags = (flags & ~Clicks) | _run switch
        {
            1 => MouseFlags.LeftButtonClicked,
            2 => MouseFlags.LeftButtonDoubleClicked,
            _ => MouseFlags.LeftButtonTripleClicked,
        };
    }
}
