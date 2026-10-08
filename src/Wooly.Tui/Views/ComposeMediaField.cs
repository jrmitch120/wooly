using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Wooly.Tui.Views;

/// <summary>
///     The Media header and the pending attachments' rows under it (#379, #377), laid over the block the compose screen
///     paints the way To's row of buttons is laid over To's: there to take the typing as the walk passes through, and
///     the mouse. It draws nothing of its own — the header and the rows, lit or barred where the typing is, are the
///     screen's rows showing through — and what a key or a click here means is the screen's, asked through the shell.
/// </summary>
/// <remarks>
///     It leaves to the window every key it does not mean to swallow, and so to <c>Keymap</c>: the arrows that walk the
///     header and the rows, <c>enter</c>, which opens the file browser on the header (#376) and describes a row,
///     <c>tab</c>, <c>esc</c>, the <c>ctrl</c> chords, and
///     <c>s</c>, the sensitive toggle. Any other letter is nobody's here — read by the keymap it would be a boost or a
///     compose behind a draft.
/// </remarks>
internal sealed class ComposeMediaField : View
{
    private readonly Action<int, int, int, bool> _clicked;

    /// <param name="clicked">
    ///     A click this many rows into the block — nought being the header — and this many columns across one this
    ///     wide; twice, for a double click.
    /// </param>
    public ComposeMediaField(Action<int, int, int, bool> clicked)
    {
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
        || !(key == Key.CursorLeft || key == Key.CursorRight || key == Key.CursorUp || key == Key.CursorDown
          || key == Key.Enter || key == Key.Tab || key == Key.Tab.WithShift || key == Key.Esc || key == Key.S
          || key.IsCtrl || key.IsAlt);

    /// <summary>
    ///     A click or a double click on the block is the screen's to answer, as a click on To is. Everything else the
    ///     pointer does here is nothing, as on the other fields: a right click (#307), and the wheel, which bubbling to
    ///     the window would read as the arrows that walk the fields.
    /// </summary>
    protected override bool OnMouseEvent(Mouse mouse)
    {
        var twice = mouse.Flags.HasFlag(MouseFlags.LeftButtonDoubleClicked);

        if (ShellKeys.Of(mouse) is null && (twice || mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked)))
        {
            if (!HasFocus)
            {
                SetFocus();
            }

            var at = FrameToScreen();

            _clicked(mouse.ScreenPosition.Y - at.Y, mouse.ScreenPosition.X - at.X, Viewport.Width, twice);
        }

        return true;
    }
}
