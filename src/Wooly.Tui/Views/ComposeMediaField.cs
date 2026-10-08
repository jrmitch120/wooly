using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Views;

/// <summary>
///     The Media header and the rows of what is attached under it (#379, #378), laid over them the way To's row of
///     buttons is laid over To's: there to take the typing as the walk passes through, and the mouse. It draws nothing of
///     its own — the header and the rows are the screen's, painted behind, lit where the typing is on them — and what a
///     key or a click on them means is the screen's, asked through the shell.
/// </summary>
/// <remarks>
///     It leaves to the window every key it does not mean to swallow, and so to <c>Keymap</c>: the arrows that walk the
///     fields and the shifted ones that reorder the rows, <c>⏎</c>, which opens the file browser there (#376),
///     <c>tab</c>, <c>esc</c>, the <c>ctrl</c> chords, <c>del</c> and <c>backspace</c>, which take a row off, <c>s</c>,
///     the sensitive toggle, and <c>r</c>, which retries a row. Any other letter is nobody's here — read by the keymap
///     it would be a boost or a compose behind a draft.
///     <para>
///         On a row the pointer picks it, takes it off by its <c>x</c>, retries it by its <c>retry (r)</c>, and drags
///         it to another place, live: the row takes each place the pointer reaches and the others make way (#378).
///         What a click there lands on is the screen's to say (<see cref="ComposeScreen.AttachmentAt" />), from the same
///         runs it draws the rows with.
///     </para>
/// </remarks>
internal sealed class ComposeMediaField : View
{
    private readonly Shell.Shell _shell;
    private readonly Action<int> _clicked;

    /// <summary>The row being dragged, from the press that picked it up to the release that lets it go.</summary>
    private ComposeAttachment? _dragging;

    /// <summary>Whether the row being dragged has moved, which makes the release a drop rather than a click.</summary>
    private bool _dragged;

    /// <param name="shell">What a click or a drag on a row is carried out by.</param>
    /// <param name="clicked">A click this many columns into the header's line.</param>
    public ComposeMediaField(Shell.Shell shell, Action<int> clicked)
    {
        _shell = shell;
        _clicked = clicked;

        CanFocus = true;
        ViewportSettings |= ViewportSettingsFlags.Transparent;
    }

    /// <summary>
    ///     First refusal on every key, ahead of everything else here: a question the shell has open on the status row,
    ///     which the key answers instead (#373).
    /// </summary>
    public Func<Key, bool>? Answering { get; set; }

    protected override bool OnKeyDown(Key key) =>
        Answering?.Invoke(key) == true
        || !(key == Key.Enter || key == Key.CursorLeft || key == Key.CursorRight || key == Key.CursorUp || key == Key.CursorDown
          || key == Key.CursorUp.WithShift || key == Key.CursorDown.WithShift || key == Key.Tab
          || key == Key.Tab.WithShift || key == Key.Esc || key == Key.Delete || key == Key.Backspace || key == Key.S
          || key == Key.R || key.IsCtrl || key.IsAlt);

    /// <summary>
    ///     The pointer on the header or the rows: a press on a row picks it and picks it up, the pointer moving with the
    ///     button held carries it to each place it reaches, and a click that was not a drag is the screen's to say the
    ///     meaning of — on the header's line, the toggle or nothing; on a row, the <c>x</c>, the retry, or the row.
    ///     Everything else the pointer does here is nothing, as on the other fields: a right click (#307), and the wheel,
    ///     which bubbling to the window would read as the arrows that walk the fields.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ShellKeys.Of(mouse) is not null)
        {
            return true;
        }

        // Where on the compose screen's rows, which this view's frame is laid in.
        var origin = FrameToScreen().Location;
        var at = new Point(mouse.ScreenPosition.X - origin.X + Frame.X, mouse.ScreenPosition.Y - origin.Y + Frame.Y);
        var flags = mouse.Flags;

        if (flags.HasFlag(MouseFlags.LeftButtonPressed) && _dragging is null)
        {
            Pressed(at);
        }
        else if (flags.HasFlag(MouseFlags.LeftButtonPressed) && _dragging is { } dragging)
        {
            _dragged |= _shell.DragAttachment(dragging, at.Y - Frame.Y - 1);
        }
        else if (flags.HasFlag(MouseFlags.LeftButtonReleased))
        {
            Let();
        }
        else if (flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            Clicked(at);
        }

        return true;
    }

    /// <summary>A press on a row: it is picked, and held for a drag until the button is let go.</summary>
    private void Pressed(Point at)
    {
        if (_shell.Screen is not ComposeScreen compose
            || compose.AttachmentAt(SuperView!.Viewport.Size, at) is not ({ } attachment, _))
        {
            return;
        }

        _shell.ChangeCompose(screen => screen.Pick(attachment));

        _dragging = attachment;
        _dragged = false;
        App?.Mouse.GrabMouse(this);
    }

    /// <summary>The button let go, which ends a drag where it is.</summary>
    private void Let()
    {
        if (_dragging is null)
        {
            return;
        }

        _dragging = null;

        if (App?.Mouse.IsGrabbed(this) == true)
        {
            App.Mouse.UngrabMouse();
        }
    }

    /// <summary>
    ///     A click, which a drag just finished is not: on the header's line it takes the typing and is the screen's to
    ///     answer, as a click on To is (#379); on a row the shell does whatever is under it (#378).
    /// </summary>
    private void Clicked(Point at)
    {
        if (_dragged)
        {
            _dragged = false;

            return;
        }

        if (_shell.Screen is not ComposeScreen compose)
        {
            return;
        }

        if (compose.AttachmentAt(SuperView!.Viewport.Size, at) is ({ } attachment, var part))
        {
            _shell.ClickAttachment(attachment, part);

            return;
        }

        if (at.Y != Frame.Y)
        {
            return;
        }

        if (!HasFocus)
        {
            SetFocus();
        }

        _clicked(at.X - Frame.X);
    }
}
