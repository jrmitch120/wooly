using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Tui.Screens;

namespace Wooly.Tui.Views;

/// <summary>
///     The Media header and the rows of what is attached under it (#378), as a place the typing and the mouse can be:
///     laid over the rows the compose screen paints, drawing nothing of its own. It holds the focus while the walk is on
///     the header or a row, so that the keys meant there reach the window and so <c>Keymap</c>; and it takes the
///     pointer, which picks a row, takes one off by its <c>x</c>, retries one by its <c>retry (r)</c>, and drags one
///     to another place, live.
/// </summary>
/// <remarks>
///     What a click lands on is the screen's to say (<see cref="ComposeScreen.AttachmentAt" />), from the same runs it
///     draws the rows with, and what it does is the shell's — the way To's row is laid out by the screen and clicked
///     through the shell (#338).
/// </remarks>
/// <param name="shell">What every click and drag is carried out by.</param>
internal sealed class ComposeMediaField : View
{
    private readonly Shell.Shell _shell;

    /// <summary>The row being dragged, from the press that picked it up to the release that lets it go.</summary>
    private ComposeAttachment? _dragging;

    /// <summary>Whether the row being dragged has moved, which makes the release a drop rather than a click.</summary>
    private bool _dragged;

    public ComposeMediaField(Shell.Shell shell)
    {
        _shell = shell;

        CanFocus = true;

        // The rows are the screen's, painted behind; this is only where the pointer and the walk land on them.
        ViewportSettings |= ViewportSettingsFlags.Transparent;
    }

    /// <summary>
    ///     First refusal on every key, ahead of everything else here: a question the shell has open on the status row,
    ///     which the key answers (#373). Every other key is left to go on up to the window.
    /// </summary>
    public Func<Key, bool>? Answering { get; set; }

    protected override bool OnKeyDown(Key key) => Answering?.Invoke(key) == true;

    /// <summary>
    ///     The pointer on the rows: a press on one picks it and picks it up, the pointer moving with the button held
    ///     carries it to each place it reaches with the others making way, and a click that was not a drag is the
    ///     screen's to say the meaning of — the <c>x</c>, the retry, or the row. A right click is <c>esc</c>, taken
    ///     ahead of this by the compose view; the wheel is nothing here.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (ShellKeys.Of(mouse) is not null)
        {
            return true;
        }

        // Where on the compose screen's rows, which this view's frame is laid in.
        var origin = FrameToScreen().Location;
        var at = new System.Drawing.Point(
            mouse.ScreenPosition.X - origin.X + Frame.X,
            mouse.ScreenPosition.Y - origin.Y + Frame.Y);
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
    private void Pressed(System.Drawing.Point at)
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

    /// <summary>A click, which a drag just finished is not: the shell does whatever is under it.</summary>
    private void Clicked(System.Drawing.Point at)
    {
        if (_dragged)
        {
            _dragged = false;

            return;
        }

        if (_shell.Screen is ComposeScreen compose
            && compose.AttachmentAt(SuperView!.Viewport.Size, at) is ({ } attachment, var part))
        {
            _shell.ClickAttachment(attachment, part);
        }
    }
}
